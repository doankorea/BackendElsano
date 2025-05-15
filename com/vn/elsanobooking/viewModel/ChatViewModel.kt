package com.vn.elsanobooking.viewModel

import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.vn.elsanobooking.config.Constants
import com.vn.elsanobooking.data.api.RetrofitInstance
import com.vn.elsanobooking.data.models.MessageViewModel
import com.vn.elsanobooking.data.models.MessagesUsersListViewModel
import com.vn.elsanobooking.data.models.SignalRChatMessage
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import okhttp3.*
import org.json.JSONArray
import org.json.JSONObject
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.UUID
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import androidx.preference.PreferenceManager
import kotlin.math.abs

// Data class cho tin nhắn
data class Message(
    val sender: String,
    val text: String,
    val isSentByUser: Boolean,
    val isActive: Boolean = true,
    val timestamp: String,
    val messageId: Int = 0,
    val rawTimestamp: Long = System.currentTimeMillis() // Store actual time for better comparison
)

@HiltViewModel
class ChatViewModel @Inject constructor() : ViewModel() {
    private val _messages = MutableStateFlow<List<Message>>(emptyList())
    val messages: StateFlow<List<Message>> = _messages

    private val _users = MutableStateFlow<List<MessagesUsersListViewModel>>(emptyList())
    val users: StateFlow<List<MessagesUsersListViewModel>> = _users

    private val _selectedUserId = MutableStateFlow(0)
    val selectedUserId: StateFlow<Int> = _selectedUserId

    private val _receiverName = MutableStateFlow("")
    val receiverName: StateFlow<String> = _receiverName

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error

    private var currentUserId: Int = 0
    private var receiverId: Int = 0

    // Cache để tránh trùng lặp tin nhắn
    private val recentlyProcessedMessages = mutableListOf<String>()

    // WebSocket connection
    private var webSocket: WebSocket? = null
    private val client = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(30, TimeUnit.SECONDS)
        .build()
    private val websocketUrl = "/mobileChatHub"
    private var successfulUrl: String? = null
    private val _isConnected = MutableStateFlow(false)
    val isConnected: StateFlow<Boolean> = _isConnected
    private var isConnecting = false
    private var reconnectAttempts = 0
    private val maxReconnectAttempts = 5
    private var reconnectJob: Job? = null
    private val pendingMessages = mutableListOf<PendingMessage>()
    private var lastMessageReceivedTime = System.currentTimeMillis()
    private var pingJob: Job? = null

    data class PendingMessage(
        val receiverId: Int,
        val content: String,
        val receiverName: String,
        val timestamp: Date = Date(),
        val tempId: Int = 0,
        val sentTime: Long = System.currentTimeMillis()
    )

    init {
        loadCurrentUserId()
        initWebSocket()
        startConnectionCheck()
        viewModelScope.launch(Dispatchers.IO) {
            while (true) {
                delay(30000)
                if (!_isConnected.value || System.currentTimeMillis() - lastMessageReceivedTime > 60000) {
                    reconnectWebSocket()
                } else {
                    sendPing()
                }
            }
        }
    }

    private fun loadCurrentUserId() {
        try {
            val sharedPreferences = PreferenceManager.getDefaultSharedPreferences(
                com.vn.elsanobooking.ElsanoBookingApplication.appContext
            )
            currentUserId = sharedPreferences.getInt("userId", 0)
        } catch (e: Exception) {
            Log.e("ChatViewModel", "Error loading currentUserId: ${e.message}", e)
        }
    }

    private fun getAuthToken(): String {
        return try {
            val sharedPreferences = PreferenceManager.getDefaultSharedPreferences(
                com.vn.elsanobooking.ElsanoBookingApplication.appContext
            )
            sharedPreferences.getString("accessToken", "") ?: ""
        } catch (e: Exception) {
            ""
        }
    }

    private fun initWebSocket() {
        try {
            if (successfulUrl != null) {
                connectToWebSocket(successfulUrl!!)
                return
            }
            connectToWebSocket(websocketUrl)
        } catch (e: Exception) {
            viewModelScope.launch {
                delay(5000)
                initWebSocket()
            }
        }
    }

    private fun connectToWebSocket(path: String) {
        if (isConnecting) return
        isConnecting = true
        _isConnected.value = false
        var wsUrl = "${Constants.BASE_URL.replace("http", "ws")}$path?userId=$currentUserId"
        val token = getAuthToken()
        if (token.isNotEmpty()) wsUrl += "&access_token=$token"
        val request = Request.Builder().url(wsUrl).build()
        val listener = object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                isConnecting = false
                _isConnected.value = true
                reconnectAttempts = 0
                successfulUrl = path
                sendHandshake()
                processPendingMessages()
            }
            override fun onMessage(webSocket: WebSocket, text: String) {
                lastMessageReceivedTime = System.currentTimeMillis()
                val messages = text.split("\u001E").filter { it.isNotEmpty() }
                for (messageText in messages) {
                    if (messageText.isBlank()) continue
                    processSingleMessage(messageText)
                }
            }
            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                _isConnected.value = false
            }
            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                isConnecting = false
                _isConnected.value = false
                if (path == successfulUrl) successfulUrl = null
                viewModelScope.launch(Dispatchers.IO) {
                    delay(5000)
                    initWebSocket()
                }
            }
        }
        webSocket = client.newWebSocket(request, listener)
    }

    fun loadUsers() {
        viewModelScope.launch {
            _isLoading.value = true
            _error.value = null
            try {
                val response = RetrofitInstance.messageApi.getConversations(currentUserId)
                val usersList = response.map { conversation ->
                    MessagesUsersListViewModel(
                        id = conversation.id,
                        userName = if (conversation.isArtist && conversation.artist != null)
                            conversation.artist.fullName else conversation.username,
                        avatar = conversation.avatar,
                        lastMessage = conversation.lastMessage,
                        lastMessageDate = conversation.lastMessageTime?.toString() ?: "",
                        unreadCount = conversation.unreadCount
                    )
                }
                _users.value = usersList
                _isLoading.value = false
            } catch (e: Exception) {
                _users.value = emptyList()
                _isLoading.value = false
                _error.value = "Không thể tải danh sách tin nhắn: ${e.message}"
            }
        }
    }

    fun selectUser(userId: Int) {
        if (_selectedUserId.value != userId) {
            _selectedUserId.value = userId
            receiverId = userId
            loadChat(userId)
        }
    }

    fun loadChat(selectedUserId: Int) {
        viewModelScope.launch {
            _isLoading.value = true
            _error.value = null
            try {
                val response = RetrofitInstance.messageApi.getMessages(currentUserId, selectedUserId)
                val contact = response.contact
                _receiverName.value = if (contact.isArtist && contact.artist != null)
                    contact.artist.fullName else contact.username
                val formattedMessages = response.messages.map { message ->
                    Message(
                        sender = if (message.isSentByMe) "Tôi" else _receiverName.value,
                        text = message.content,
                        isSentByUser = message.isSentByMe,
                        isActive = true,
                        timestamp = formatMessageTime(message.timestamp),
                        messageId = message.id,
                        rawTimestamp = System.currentTimeMillis()
                    )
                }
                _messages.value = formattedMessages.reversed() // Đảo ngược để tin nhắn mới nhất ở cuối
                _isLoading.value = false
            } catch (e: Exception) {
                _messages.value = emptyList()
                _isLoading.value = false
                _error.value = "Không thể tải tin nhắn: ${e.message}"
            }
        }
    }

    private fun formatMessageTime(timestamp: String): String {
        return try {
            val date = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.getDefault()).parse(timestamp)
            if (date != null) SimpleDateFormat("HH:mm", Locale.getDefault()).format(date) else "N/A"
        } catch (e: Exception) {
            "N/A"
        }
    }

    fun sendMessage(messageText: String, receiverName: String) {
        viewModelScope.launch {
            val targetReceiverId = if (receiverId > 0) receiverId else selectedUserId.value
            if (messageText.isBlank() || targetReceiverId <= 0) return@launch
            val tempId = UUID.randomUUID().toString().hashCode()
            val now = System.currentTimeMillis()
            val newMessage = Message(
                sender = "Tôi",
                text = messageText,
                isSentByUser = true,
                isActive = false,
                timestamp = SimpleDateFormat("HH:mm", Locale.getDefault()).format(Date()),
                messageId = tempId,
                rawTimestamp = now
            )
            _messages.value = _messages.value + newMessage // Thêm vào cuối (do reversed ở loadChat)
            sendMessageInternal(messageText, receiverName, targetReceiverId, tempId, now)
        }
    }

    private fun sendMessageInternal(
        messageText: String, receiverName: String, toReceiverId: Int, tempId: Int, sentTime: Long
    ) {
        var webSocketSuccess = false
        if (webSocket != null && _isConnected.value) {
            val messageObj = JSONObject()
            messageObj.put("type", 1)
            messageObj.put("target", "SendMessage")
            val args = JSONArray()
            args.put(currentUserId)
            args.put(toReceiverId)
            args.put(messageText)
            messageObj.put("arguments", args)
            val invocationId = UUID.randomUUID().toString().substring(0, 8)
            messageObj.put("invocationId", invocationId)
            val signalRMessage = messageObj.toString() + "\u001E"
            webSocketSuccess = webSocket?.send(signalRMessage) ?: false
        }
        if (!webSocketSuccess) {
            pendingMessages.add(PendingMessage(toReceiverId, messageText, receiverName, Date(), tempId, sentTime))
            if (webSocket == null || !_isConnected.value) connectWebSocketAndSendPendingMessages()
        }
        sendMessageREST(toReceiverId, messageText)
    }

    private fun sendMessageREST(receiverId: Int, content: String) {
        if (content.isBlank()) return
        val finalContent = if (content.length < 2) "$content " else content
        viewModelScope.launch(Dispatchers.IO) {
            try {
                val messageRequest = SignalRChatMessage(
                    senderId = currentUserId,
                    receiverId = receiverId,
                    content = finalContent,
                    timestamp = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.getDefault()).format(Date())
                )
                RetrofitInstance.messageApi.sendMessage(messageRequest)
            } catch (_: Exception) {}
        }
    }

    private fun processPendingMessages() {
        if (pendingMessages.isEmpty()) return
        val iterator = pendingMessages.iterator()
        while (iterator.hasNext()) {
            val message = iterator.next()
            sendMessageInternal(message.content, message.receiverName, message.receiverId, message.tempId, message.sentTime)
            iterator.remove()
        }
    }

    private fun processSingleMessage(messageText: String) {
        if (messageText.isBlank()) return
        try {
            lastMessageReceivedTime = System.currentTimeMillis()
            val json = JSONObject(messageText)
            if (json.has("type") && json.getInt("type") == 1 && json.has("target")) {
                val target = json.getString("target")
                if (target.equals("ReceiveMessage", true)) {
                    if (json.has("arguments")) {
                        val args = json.getJSONArray("arguments")
                        if (args.length() >= 7) {
                            val messageId = args.getInt(0)
                            val senderId = args.getInt(1)
                            val receiverId = args.getInt(2)
                            val content = args.getString(3)
                            val date = args.getString(4)
                            val time = args.getString(5)
                            val isSentByMe = args.getBoolean(6)
                            processNewMessage(messageId, senderId, receiverId, content, date, time, isSentByMe)
                        }
                    }
                }
            }
        } catch (_: Exception) {}
    }

    private fun processNewMessage(
        messageId: Int, senderId: Int, receiverId: Int, content: String, date: String, time: String, isSentByMe: Boolean
    ) {
        val now = System.currentTimeMillis()
        viewModelScope.launch(Dispatchers.Main.immediate) {
            val currentList = _messages.value.toMutableList()
            if (isSentByMe) {
                val idx = currentList.indexOfFirst {
                    it.isSentByUser && it.text == content && abs(now - it.rawTimestamp) < 10000
                }
                if (idx >= 0) {
                    currentList[idx] = currentList[idx].copy(
                        messageId = messageId,
                        isActive = true
                    )
                } else {
                    currentList.add(Message(
                        sender = "Tôi",
                        text = content,
                        isSentByUser = true,
                        isActive = true,
                        timestamp = time,
                        messageId = messageId,
                        rawTimestamp = now
                    ))
                }
            } else {
                currentList.add(Message(
                    sender = _receiverName.value.ifEmpty { "Người dùng #$senderId" },
                    text = content,
                    isSentByUser = false,
                    isActive = true,
                    timestamp = time,
                    messageId = messageId,
                    rawTimestamp = now
                ))
            }
            _messages.value = currentList
        }
    }

    fun reconnectWebSocket() {
        reconnectJob?.cancel()
        webSocket?.cancel()
        webSocket = null
        reconnectAttempts = 0
        _isConnected.value = false
        reconnectJob = viewModelScope.launch {
            var delayTime = 1000L
            while (reconnectAttempts < maxReconnectAttempts && !_isConnected.value) {
                reconnectAttempts++
                delay(delayTime)
                initWebSocket()
                delay(3000)
                if (!_isConnected.value) {
                    delayTime = minOf(delayTime * 2, 10000)
                } else break
            }
        }
    }

    private fun connectWebSocketAndSendPendingMessages() {
        webSocket?.close(1000, "Closing previous connection")
        webSocket = null
        if (isConnecting) return
        isConnecting = true
        initWebSocket()
    }

    private fun sendHandshake() {
        try {
            val handshakeJson = "{\"protocol\":\"json\",\"version\":1}\u001E"
            webSocket?.send(handshakeJson)
            subscribeToHub()
            viewModelScope.launch(Dispatchers.Main.immediate) { _isConnected.value = true }
        } catch (_: Exception) {
            webSocket?.cancel()
            webSocket = null
            viewModelScope.launch { delay(1000); initWebSocket() }
        }
    }

    private fun subscribeToHub() {
        // SignalR sẽ tự động route các tin nhắn đến client nếu client có phương thức xử lý tương ứng
    }

    private fun startConnectionCheck() {
        viewModelScope.launch {
            while (true) {
                delay(30000)
                checkConnection()
            }
        }
    }

    private fun checkConnection() {
        if (webSocket == null) {
            initWebSocket()
            return
        }
        try {
            val ping = JSONObject()
            ping.put("type", 6)
            val pingStr = ping.toString() + "\u001E"
            val success = webSocket?.send(pingStr) ?: false
            if (!success) {
                webSocket?.cancel()
                webSocket = null
                initWebSocket()
            }
        } catch (_: Exception) {
            webSocket?.cancel()
            webSocket = null
            initWebSocket()
        }
    }

    private fun sendPing() {
        if (webSocket != null && _isConnected.value) {
            try {
                val ping = JSONObject()
                ping.put("type", 6)
                val pingStr = ping.toString() + "\u001E"
                webSocket?.send(pingStr)
            } catch (_: Exception) {}
        }
    }

    override fun onCleared() {
        super.onCleared()
        try {
            pingJob?.cancel()
            webSocket?.close(1000, "View model cleared")
        } catch (_: Exception) {}
    }
} 