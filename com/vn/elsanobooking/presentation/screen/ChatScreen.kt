package com.vn.elsanobooking.presentation.screen

import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Send
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.viewmodel.compose.viewModel
import coil.compose.AsyncImage
import com.vn.elsanobooking.data.models.MessagesUsersListViewModel
import com.vn.elsanobooking.data.models.User
import com.vn.elsanobooking.viewModel.ChatViewModel
import com.vn.elsanobooking.viewModel.Message
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

@Composable
fun ChatScreen(
    modifier: Modifier = Modifier,
    onBackClick: () -> Unit = {},
    viewModel: ChatViewModel = viewModel(),
    showUserList: Boolean = true
) {
    val messages by viewModel.messages.collectAsState()
    val receiverName by viewModel.receiverName.collectAsState()
    val users by viewModel.users.collectAsState()
    val selectedUserId by viewModel.selectedUserId.collectAsState()
    val isLoading by viewModel.isLoading.collectAsState()
    val error by viewModel.error.collectAsState()
    val isConnected by viewModel.isConnected.collectAsState()

    var messageInput by remember { mutableStateOf("") }
    val listState = rememberLazyListState()
    val coroutineScope = rememberCoroutineScope()

    // Scroll to latest message when messages change
    LaunchedEffect(messages.size) {
        if (messages.isNotEmpty()) {
            coroutineScope.launch {
                listState.animateScrollToItem(messages.size - 1)
            }
        }
    }

    if (showUserList) {
        Row(modifier = modifier.fillMaxSize()) {
            // User List (left side)
            Column(
                modifier = Modifier
                    .weight(0.4f)
                    .fillMaxHeight()
                    .background(Color.White)
            ) {
                Text(
                    text = "Cuộc trò chuyện",
                    fontWeight = FontWeight.Bold,
                    fontSize = 20.sp,
                    modifier = Modifier.padding(16.dp)
                )

                if (isLoading && users.isEmpty()) {
                    Box(
                        modifier = Modifier.fillMaxSize(),
                        contentAlignment = Alignment.Center
                    ) {
                        CircularProgressIndicator()
                    }
                } else {
                    LazyColumn {
                        items(users) { user ->
                            UserItem(
                                user = user,
                                isSelected = user.id == selectedUserId,
                                onClick = { viewModel.selectUser(user.id) }
                            )
                        }
                    }
                }
            }

            // Chat Interface (right side)
            Column(
                modifier = Modifier
                    .weight(0.6f)
                    .fillMaxHeight()
            ) {
                if (selectedUserId != 0) {
                    ChatHeader(
                        user = User(
                            id = selectedUserId,
                            name = receiverName,
                            isActive = true,
                            avatar = users.find { it.id == selectedUserId }?.avatar
                        ),
                        onBackClick = onBackClick
                    )

                    if (isLoading && messages.isEmpty()) {
                        Box(
                            modifier = Modifier.fillMaxSize(),
                            contentAlignment = Alignment.Center
                        ) {
                            CircularProgressIndicator()
                        }
                    } else {
                        ChatContent(
                            messages = messages,
                            receiverName = receiverName,
                            users = users,
                            listState = listState,
                            messageInput = messageInput,
                            onMessageChange = { messageInput = it },
                            onSendClick = {
                                if (messageInput.isNotBlank()) {
                                    viewModel.sendMessage(messageInput, receiverName)
                                    messageInput = ""
                                }
                            },
                            onReconnectClick = { viewModel.reconnectWebSocket() },
                            isConnected = isConnected
                        )
                    }
                } else {
                    Box(
                        modifier = Modifier.fillMaxSize(),
                        contentAlignment = Alignment.Center
                    ) {
                        Text("Chọn một cuộc trò chuyện để bắt đầu", color = Color.Gray)
                    }
                }
            }
        }
    } else {
        Column(
            modifier = modifier.fillMaxSize()
        ) {
            ChatHeader(
                user = User(
                    id = selectedUserId,
                    name = receiverName,
                    isActive = true,
                    avatar = users.find { it.id == selectedUserId }?.avatar
                ),
                onBackClick = onBackClick
            )

            if (isLoading && messages.isEmpty()) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    CircularProgressIndicator()
                }
            } else {
                ChatContent(
                    messages = messages,
                    receiverName = receiverName,
                    users = users,
                    listState = listState,
                    messageInput = messageInput,
                    onMessageChange = { messageInput = it },
                    onSendClick = {
                        if (messageInput.isNotBlank()) {
                            viewModel.sendMessage(messageInput, receiverName)
                            messageInput = ""
                        }
                    },
                    onReconnectClick = { viewModel.reconnectWebSocket() },
                    isConnected = isConnected
                )
            }
        }
    }

    error?.let { errorMessage ->
        Snackbar(
            modifier = Modifier.padding(16.dp),
            action = {
                TextButton(onClick = { /* Clear error */ }) {
                    Text("Đóng")
                }
            }
        ) {
            Text(errorMessage)
        }
    }
}

@Composable
fun ChatContent(
    messages: List<Message>,
    receiverName: String,
    users: List<MessagesUsersListViewModel>,
    listState: androidx.compose.foundation.lazy.LazyListState,
    messageInput: String,
    onMessageChange: (String) -> Unit,
    onSendClick: () -> Unit,
    onReconnectClick: () -> Unit,
    isConnected: Boolean
) {
    Column(
        modifier = Modifier.fillMaxSize()
    ) {
        if (!isConnected) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(Color(0xFFFFCDD2))
                    .padding(8.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Mất kết nối với máy chủ",
                    color = Color.Red
                )
                Button(
                    onClick = onReconnectClick,
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Color.Red
                    )
                ) {
                    Text("Kết nối lại")
                }
            }
        }

        LazyColumn(
            state = listState,
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .background(Color(0xFFF5F5F5))
                .padding(horizontal = 16.dp),
            reverseLayout = false
        ) {
            items(messages) { message ->
                MessageItem(message = message, users = users)
            }
        }

        ChatInput(
            messageInput = messageInput,
            onMessageChange = onMessageChange,
            onSendClick = onSendClick
        )
    }
}

@Composable
fun UserItem(user: MessagesUsersListViewModel, isSelected: Boolean, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .background(if (isSelected) Color(0xFFE3F2FD) else Color.White)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        AsyncImage(
            model = user.avatar,
            contentDescription = "${user.userName} avatar",
            modifier = Modifier
                .size(40.dp)
                .clip(CircleShape)
                .background(Color.Gray),
            placeholder = painterResource(id = android.R.drawable.ic_menu_gallery),
            error = painterResource(id = android.R.drawable.ic_menu_gallery)
        )
        Spacer(modifier = Modifier.width(12.dp))
        Column {
            Text(
                text = user.userName,
                fontWeight = FontWeight.Bold,
                fontSize = 16.sp
            )
            Text(
                text = user.lastMessage.toString(),
                fontSize = 14.sp,
                color = Color.Gray
            )
        }
    }
}

@Composable
fun ChatHeader(user: User, onBackClick: () -> Unit) {
    val viewModel: ChatViewModel = viewModel()
    val isConnected by viewModel.isConnected.collectAsState()

    TopAppBar(
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                AsyncImage(
                    model = user.avatar,
                    contentDescription = "${user.name} avatar",
                    modifier = Modifier
                        .size(40.dp)
                        .clip(CircleShape)
                        .background(Color.Gray)
                        .shadow(4.dp, CircleShape),
                    placeholder = painterResource(id = android.R.drawable.ic_menu_gallery),
                    error = painterResource(id = android.R.drawable.ic_menu_gallery)
                )
                Spacer(modifier = Modifier.width(12.dp))
                Column {
                    Text(
                        text = user.name,
                        fontWeight = FontWeight.Bold,
                        fontSize = 18.sp
                    )
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            modifier = Modifier
                                .size(10.dp)
                                .clip(CircleShape)
                                .background(if (user.isActive) Color.Green else Color.Gray)
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(
                            text = if (user.isActive) "Đang hoạt động" else "Không hoạt động",
                            fontSize = 14.sp,
                            color = Color.Gray
                        )
                    }
                }
            }
        },
        navigationIcon = {
            IconButton(onClick = onBackClick) {
                Icon(Icons.Default.ArrowBack, contentDescription = "Back")
            }
        },
        actions = {
            Box(
                modifier = Modifier
                    .size(12.dp)
                    .clip(CircleShape)
                    .background(if (isConnected) Color.Green else Color.Red)
            )
            Spacer(modifier = Modifier.width(8.dp))
            IconButton(onClick = {
                if (!isConnected) {
                    viewModel.reconnectWebSocket()
                }
            }) {
                Icon(Icons.Default.MoreVert, contentDescription = "More")
            }
        },
        colors = TopAppBarDefaults.topAppBarColors(containerColor = Color.White)
    )
}

@Composable
fun MessageItem(message: Message, users: List<MessagesUsersListViewModel>) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp),
        horizontalArrangement = if (message.isSentByUser) Arrangement.End else Arrangement.Start
    ) {
        if (!message.isSentByUser) {
            AsyncImage(
                model = users.find { it.userName == message.sender }?.avatar,
                contentDescription = "${message.sender} avatar",
                modifier = Modifier
                    .size(36.dp)
                    .clip(CircleShape)
                    .background(if (message.isActive) Color(0xFF4CAF50) else Color.Gray),
                placeholder = painterResource(id = android.R.drawable.ic_menu_gallery),
                error = painterResource(id = android.R.drawable.ic_menu_gallery)
            )
            Spacer(modifier = Modifier.width(12.dp))
        }

        Column(horizontalAlignment = if (message.isSentByUser) Alignment.End else Alignment.Start) {
            if (!message.isSentByUser) {
                Text(
                    text = message.sender,
                    fontSize = 12.sp,
                    color = Color.Gray,
                    modifier = Modifier.padding(bottom = 4.dp)
                )
            }
            Card(
                shape = RoundedCornerShape(20.dp),
                colors = CardDefaults.cardColors(
                    containerColor = if (message.isSentByUser) Color(0xFF007AFF) else Color.White
                ),
                elevation = CardDefaults.cardElevation(defaultElevation = 4.dp),
                modifier = Modifier.widthIn(max = 300.dp)
            ) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Text(
                        text = message.text,
                        color = if (message.isSentByUser) Color.White else Color.Black,
                        fontSize = 16.sp
                    )
                    Text(
                        text = message.timestamp,
                        color = if (message.isSentByUser) Color(0xCCFFFFFF) else Color.Gray,
                        fontSize = 12.sp,
                        modifier = Modifier
                            .padding(top = 4.dp)
                            .align(Alignment.End)
                    )
                }
            }
        }
    }
}

@Composable
fun ChatInput(
    messageInput: String,
    onMessageChange: (String) -> Unit,
    onSendClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(Color.White)
            .padding(horizontal = 8.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        TextField(
            value = messageInput,
            onValueChange = onMessageChange,
            placeholder = { Text("Nhập tin nhắn...") },
            modifier = Modifier
                .weight(1f)
                .clip(RoundedCornerShape(24.dp))
                .background(Color(0xFFF0F0F0)),
            colors = TextFieldDefaults.colors(
                focusedIndicatorColor = Color.Transparent,
                unfocusedIndicatorColor = Color.Transparent,
                focusedContainerColor = Color(0xFFF0F0F0),
                unfocusedContainerColor = Color(0xFFF0F0F0)
            )
        )
        Spacer(modifier = Modifier.width(8.dp))
        IconButton(
            onClick = onSendClick,
            modifier = Modifier
                .size(48.dp)
                .clip(CircleShape)
                .background(Color(0xFF007AFF))
        ) {
            Icon(
                imageVector = Icons.Default.Send,
                contentDescription = "Gửi",
                tint = Color.White
            )
        }
    }
} 