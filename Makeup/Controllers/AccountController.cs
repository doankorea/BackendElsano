using Makeup.Models;
using Makeup.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text;

namespace Makeup.Controllers
{
    public class AccountController : Controller
    {
        private UserManager<User> _userManage;
        private SignInManager<User> _signInManager;
        private RoleManager<IdentityRole<int>> _roleManager;
        private readonly IEmailSender _emailSender;

        private readonly MakeupContext _dataContext;
        public AccountController(UserManager<User> userManage,
            SignInManager<User> signInManager, MakeupContext context, 
            RoleManager<IdentityRole<int>> roleManager,
            IEmailSender emailSender)
        {
            _userManage = userManage;
            _signInManager = signInManager;
            _dataContext = context;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterVM user)
        {
            if (ModelState.IsValid)
            {
                User newUser = new User { UserName = user.UserName, Email = user.Email };
                IdentityResult result = await _userManage.CreateAsync(newUser, user.Password);
                if (result.Succeeded)
                {
                    var roleExists = await _roleManager.RoleExistsAsync("Artist");
                    if (!roleExists)
                    {
                        // Nếu vai trò chưa tồn tại, tạo mới vai trò "Artist"
                        await _roleManager.CreateAsync(new IdentityRole<int>("Artist"));
                    }
                    var addRoleResult = await _userManage.AddToRoleAsync(newUser, "Artist");
                    
                    // Tạo token xác nhận email
                    var token = await _userManage.GenerateEmailConfirmationTokenAsync(newUser);
                    
                    // Tạo URL xác nhận
                    var confirmationLink = Url.Action("ConfirmEmail", "Account",
                        new { userId = newUser.Id, token = token }, Request.Scheme);
                    
                    // Tạo nội dung email
                    var emailSubject = "Xác nhận tài khoản - Makeup Artist Platform";
                    var emailBody = BuildEmailTemplate(newUser.UserName, confirmationLink);
                    
                    try
                    {
                        // Gửi email
                        await _emailSender.SendEmailAsync(user.Email, emailSubject, emailBody);
                        
                        TempData["success"] = "Đăng ký thành công. Vui lòng kiểm tra email để xác nhận tài khoản.";
                        return RedirectToAction("RegisterConfirmation", new { email = user.Email });
                    }
                    catch (Exception ex)
                    {
                        // Vẫn cho phép đăng ký thành công nhưng hiển thị lỗi gửi email
                        TempData["warning"] = "Đăng ký thành công nhưng không thể gửi email xác nhận. Lỗi: " + ex.Message;
                        return RedirectToAction("RegisterConfirmation", new { email = user.Email });
                    }
                }
                foreach (IdentityError error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(user);
        }

        public IActionResult RegisterConfirmation(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManage.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Không thể tải người dùng với ID '{userId}'.");
            }

            var result = await _userManage.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Lỗi khi xác nhận email cho người dùng với ID '{userId}':");
            }

            TempData["success"] = "Xác nhận email thành công! Bạn đã có thể đăng nhập.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ResendEmailConfirmation()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailConfirmation(ResendEmailConfirmationVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManage.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy người dùng với email này.");
                return View(model);
            }

            if (await _userManage.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError(string.Empty, "Email này đã được xác nhận.");
                return View(model);
            }

            var token = await _userManage.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Account",
                new { userId = user.Id, token = token }, Request.Scheme);

            var emailSubject = "Xác nhận tài khoản - Makeup Artist Platform";
            var emailBody = BuildEmailTemplate(user.UserName, confirmationLink);

            try
            {
                await _emailSender.SendEmailAsync(model.Email, emailSubject, emailBody);
                TempData["success"] = "Email xác nhận đã được gửi lại. Vui lòng kiểm tra hộp thư của bạn.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Không thể gửi email. Lỗi: " + ex.Message);
                return View(model);
            }
        }

        private string BuildEmailTemplate(string userName, string confirmationLink)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("<title>Xác nhận tài khoản</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }");
            sb.AppendLine(".container { border: 1px solid #ddd; border-radius: 5px; padding: 20px; background-color: #f9f9f9; }");
            sb.AppendLine(".logo { text-align: center; margin-bottom: 20px; }");
            sb.AppendLine(".button { display: inline-block; background-color: #e91e63; color: white; padding: 10px 20px; text-decoration: none; border-radius: 4px; margin: 20px 0; }");
            sb.AppendLine(".footer { margin-top: 30px; font-size: 12px; color: #777; text-align: center; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"container\">");
            sb.AppendLine("<div class=\"logo\"><h2>Makeup Artist Platform</h2></div>");
            sb.AppendLine($"<p>Xin chào <strong>{userName}</strong>,</p>");
            sb.AppendLine("<p>Cảm ơn bạn đã đăng ký tài khoản trên Makeup Artist Platform. Để hoàn tất quá trình đăng ký, vui lòng xác nhận địa chỉ email của bạn bằng cách nhấp vào nút bên dưới:</p>");
            sb.AppendLine($"<p style=\"text-align: center;\"><a href=\"{HtmlEncoder.Default.Encode(confirmationLink)}\" class=\"button\">Xác nhận Email</a></p>");
            sb.AppendLine("<p>Hoặc bạn có thể sao chép và dán liên kết sau vào trình duyệt:</p>");
            sb.AppendLine($"<p>{HtmlEncoder.Default.Encode(confirmationLink)}</p>");
            sb.AppendLine("<p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này.</p>");
            sb.AppendLine("<p>Trân trọng,<br>Đội ngũ Makeup Artist Platform</p>");
            sb.AppendLine("<div class=\"footer\">");
            sb.AppendLine("<p>© 2023 Makeup Artist Platform. Tất cả các quyền được bảo lưu.</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Login(string returnUrl = "/")
        {
            return View(new LoginVM { ReturnUrl = returnUrl ?? "/" });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM loginVM)
        {
            // Ensure ReturnUrl is never null
            loginVM.ReturnUrl = loginVM.ReturnUrl ?? "/";
            
            Console.WriteLine("Request tới action Login với UserName: " + loginVM?.UserName);

            if (ModelState.IsValid)
            {
                // Kiểm tra nếu người dùng đã xác nhận email chưa
                var userCheck = await _userManage.FindByNameAsync(loginVM.UserName);
                if (userCheck == null)
                {
                    Console.WriteLine("Không tìm thấy người dùng với username: " + loginVM.UserName);
                    ModelState.AddModelError("", "Sai tài khoản hoặc mật khẩu.");
                    return View(loginVM);
                }
                
                Console.WriteLine($"Tìm thấy người dùng {userCheck.UserName}, Email: {userCheck.Email}, EmailConfirmed: {userCheck.EmailConfirmed}");
                
                if (!await _userManage.IsEmailConfirmedAsync(userCheck))
                {
                    Console.WriteLine("Email chưa được xác nhận");
                    ModelState.AddModelError("", "Tài khoản chưa được xác nhận email. Vui lòng kiểm tra hộp thư của bạn.");
                    ViewBag.ShowResendLink = true;
                    ViewBag.UserEmail = userCheck.Email;
                    return View(loginVM);
                }

                Console.WriteLine("ModelState hợp lệ, bắt đầu đăng nhập...");
                var result = await _signInManager.PasswordSignInAsync(loginVM.UserName, loginVM.Password, isPersistent: false, lockoutOnFailure: false);
                Console.WriteLine($"Kết quả đăng nhập: Succeeded={result.Succeeded}, IsLockedOut={result.IsLockedOut}, IsNotAllowed={result.IsNotAllowed}, RequiresTwoFactor={result.RequiresTwoFactor}");
                
                if (result.Succeeded)
                {
                    var user = await _userManage.FindByNameAsync(loginVM.UserName);
                    if (user == null)
                    {
                        ModelState.AddModelError("", "Không tìm thấy người dùng.");
                        return View(loginVM);
                    }

                    Console.WriteLine($"Đăng nhập thành công cho userId: {user.Id}, ReturnUrl: {loginVM.ReturnUrl}");

                    try
                    {
                        // Kiểm tra vai trò và chuyển hướng
                        if (await _userManage.IsInRoleAsync(user, "Admin"))
                        {
                            Console.WriteLine("Người dùng có vai trò Admin");
                            // Luôn chuyển hướng cố định để test
                            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                        }

                        // Check if user has the "Artist" role
                        if (await _userManage.IsInRoleAsync(user, "Artist"))
                        {
                            Console.WriteLine("Người dùng có vai trò Artist");
                            
                            // Check if MakeupArtist record exists
                            var artistExists = await _dataContext.MakeupArtists
                                .AnyAsync(a => a.UserId == user.Id);

                            Console.WriteLine($"MakeupArtist record exists: {artistExists}");

                            if (!artistExists)
                            {
                                // Rút gọn phần tạo MakeupArtist mới để tránh lỗi
                                try 
                                {
                                    var newArtist = new MakeupArtist
                                    {
                                        UserId = user.Id,
                                        FullName = user.UserName,
                                        IsActive = 1,
                                        Rating = 5.0m,
                                        ReviewsCount = 1,
                                        IsAvailableAtHome = 0
                                    };

                                    _dataContext.MakeupArtists.Add(newArtist);
                                    await _dataContext.SaveChangesAsync();
                                    Console.WriteLine("Đã tạo hồ sơ nghệ sĩ mới");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Lỗi khi tạo hồ sơ nghệ sĩ: {ex.Message}");
                                }
                            }
                        }

                        // Chuyển hướng đơn giản để test
                        Console.WriteLine("Chuyển hướng đến trang chủ");
                        return RedirectToAction("Index", "Home");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi khi chuyển hướng: {ex.Message}");
                        // Nếu có lỗi, vẫn cố gắng chuyển hướng về trang chủ
                        return RedirectToAction("Index", "Home");
                    }
                }
                else
                {
                    Console.WriteLine("Đăng nhập thất bại:");
                    if (result.IsLockedOut)
                    {
                        Console.WriteLine("Tài khoản bị khóa");
                        ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa.");
                    }
                    else if (result.IsNotAllowed)
                    {
                        Console.WriteLine("Đăng nhập không được phép");
                        ModelState.AddModelError("", "Đăng nhập không được phép.");
                    }
                    else if (result.RequiresTwoFactor)
                    {
                        Console.WriteLine("Yêu cầu xác thực hai yếu tố");
                        ModelState.AddModelError("", "Yêu cầu xác thực hai yếu tố.");
                    }
                    else
                    {
                        Console.WriteLine("Sai tài khoản hoặc mật khẩu");
                        ModelState.AddModelError("", "Sai tài khoản hoặc mật khẩu.");
                    }
                }
            }
            else
            {
                Console.WriteLine("ModelState không hợp lệ:");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine("Lỗi: " + error.ErrorMessage);
                }
            }
            return View(loginVM);
        }

        public async Task<IActionResult> Logout(string returnUrl = "/")
        {
            await _signInManager.SignOutAsync();
            await HttpContext.SignOutAsync();
            return Redirect(returnUrl);
        }












        
        [HttpPost("Account/CreateUser")]
        public async Task<IActionResult> CreateUser([FromBody] RegisterVM model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var newUser = new User
            {
                UserName = model.UserName,
                Email = model.Email
            };

            var result = await _userManage.CreateAsync(newUser, model.Password);
            if (result.Succeeded)
            {
                // Ensure "User" role exists
                var roleExists = await _roleManager.RoleExistsAsync("User");
                if (!roleExists)
                {
                    await _roleManager.CreateAsync(new IdentityRole<int>("User"));
                }

                // Assign "User" role
                var addRoleResult = await _userManage.AddToRoleAsync(newUser, "User");
                if (addRoleResult.Succeeded)
                {
                    return Ok(new
                    {
                        Status = "Success",
                        Message = "Tạo thành viên thành công",
                        UserId = newUser.Id
                    });
                }

                // Rollback user creation if role assignment fails (optional, depending on requirements)
                await _userManage.DeleteAsync(newUser);
                return BadRequest(new
                {
                    Status = "Error",
                    Errors = addRoleResult.Errors.Select(e => e.Description)
                });
            }

            return BadRequest(new
            {
                Status = "Error",
                Errors = result.Errors.Select(e => e.Description)
            });
        }
        [HttpPost("Account/LoginUser")]
        public async Task<IActionResult> LoginUser([FromBody] LoginVM model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var user = await _userManage.FindByNameAsync(model.UserName);
            if (user == null)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Message = "Không tìm thấy người dùng."
                });
            }

            // Kiểm tra xác nhận email
            if (!await _userManage.IsEmailConfirmedAsync(user))
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Message = "Tài khoản chưa được xác nhận email. Vui lòng kiểm tra hộp thư của bạn.",
                    RequireEmailConfirmation = true,
                    Email = user.Email
                });
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.UserName,
                model.Password,
                isPersistent: false,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // Return user info (mimicking session establishment)
                return Ok(new
                {
                    Status = "Success",
                    User = new
                    {
                        user.UserName,
                        user.Email,
                        UserId = user.Id,
                        Roles = await _userManage.GetRolesAsync(user),
                        RequireEmailConfirmation = true
                    }
                });
            }

            if (result.IsLockedOut)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Message = "Tài khoản của bạn đã bị khóa."
                });
            }
            if (result.IsNotAllowed)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Message = "Đăng nhập không được phép."
                });
            }
            if (result.RequiresTwoFactor)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Message = "Yêu cầu xác thực hai yếu tố."
                });
            }

            return BadRequest(new
            {
                Status = "Error",
                Message = "Sai tài khoản hoặc mật khẩu."
            });
        }
        [HttpPost("Account/LogoutUser")]
        public async Task<IActionResult> LogoutUser()
        {
            await _signInManager.SignOutAsync();
            return Ok(new
            {
                Status = "Success",
                Message = "Đăng xuất thành công"
            });
        }
        
        // Phương thức tạm thời để kiểm tra và xác nhận email trực tiếp
        public async Task<IActionResult> DebugUser(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return Content("Vui lòng nhập username");
            }

            var user = await _userManage.FindByNameAsync(username);
            if (user == null)
            {
                return Content($"Không tìm thấy người dùng với username: {username}");
            }

            var message = $"Thông tin người dùng:\n" +
                          $"Id: {user.Id}\n" +
                          $"Username: {user.UserName}\n" +
                          $"Email: {user.Email}\n" +
                          $"EmailConfirmed: {user.EmailConfirmed}\n";

            if (!user.EmailConfirmed)
            {
                // Xác nhận email
                var token = await _userManage.GenerateEmailConfirmationTokenAsync(user);
                var result = await _userManage.ConfirmEmailAsync(user, token);
                
                if (result.Succeeded)
                {
                    message += "Email đã được xác nhận thành công.";
                }
                else
                {
                    message += $"Không thể xác nhận email: {string.Join(", ", result.Errors.Select(e => e.Description))}";
                }
            }

            // Kiểm tra vai trò
            var roles = await _userManage.GetRolesAsync(user);
            message += $"\nVai trò: {string.Join(", ", roles)}";

            return Content(message);
        }

        // Phương thức kiểm tra trạng thái đăng nhập
        public IActionResult CheckLoginStatus()
        {
            var isAuthenticated = User.Identity.IsAuthenticated;
            var userId = _userManage.GetUserId(User);
            var userName = User.Identity.Name;
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            
            var message = $"Đã đăng nhập: {isAuthenticated}\n" +
                          $"UserId: {userId}\n" +
                          $"UserName: {userName}\n" +
                          $"Claims: {string.Join(", ", claims.Select(c => $"{c.Type}={c.Value}"))}";
                          
            return Content(message);
        }

        [HttpPost("Account/ResendConfirmationEmail")]
        public async Task<IActionResult> ResendConfirmationEmailApi([FromBody] ResendEmailConfirmationVM model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            var user = await _userManage.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Message = "Không tìm thấy người dùng với email này."
                });
            }

            if (await _userManage.IsEmailConfirmedAsync(user))
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Message = "Email này đã được xác nhận."
                });
            }

            var token = await _userManage.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Account",
                new { userId = user.Id, token = token }, Request.Scheme);

            var emailSubject = "Xác nhận tài khoản - Makeup Artist Platform";
            var emailBody = BuildEmailTemplate(user.UserName, confirmationLink);

            try
            {
                await _emailSender.SendEmailAsync(model.Email, emailSubject, emailBody);
                return Ok(new
                {
                    Status = "Success",
                    Message = "Email xác nhận đã được gửi lại. Vui lòng kiểm tra hộp thư của bạn."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Message = "Không thể gửi email. Lỗi: " + ex.Message
                });
            }
        }
    }
}
