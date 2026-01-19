using System;
using System.Collections.Generic;


using AutoMapper;

using System.Security.Cryptography;
using System.Text;
using System.Security.Cryptography;
using System.Text;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    // Application service translates domain entities to DTOs and uses UnitOfWork
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;       
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;

        public UserService(IUnitOfWork unitOfWork, IUserRepository userRepository, IEmailService emailService, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _userRepository = userRepository;
            _emailService = emailService;
            _mapper = mapper;

        }

        public IEnumerable<UserDto> GetAll()
        {
            var users = _unitOfWork.Users.GetAll();
            return _mapper.Map<IEnumerable<UserDto>>(users);        
        }

        public LoginResponse? Login(LoginRequest request)
        {
            var user = _userRepository.GetByEmail(request.Email);
            if (user == null) return null;

            if (!user.EmailConfirmed)
            {
                // Note : Ch?a bi?t return v? gì
                return null; 
            }

            if (!VerifyPasswordHash(request.Password, user.HashedPassword))
            {
                return null;
            }
          
            var token = "mock-token-" + Guid.NewGuid(); 
            
            // Generate Refresh Token
            var refreshTokenRaw = Guid.NewGuid().ToString();
            var refreshTokenHash = CreatePasswordHash(refreshTokenRaw);
            var refreshTokenParts = refreshTokenHash.Split(':');
            
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = refreshTokenParts[1],
                TokenSalt = refreshTokenParts[0],
                CreatedAt = DateTime.UtcNow
            };
            
            
            // FIX: Using AddRefreshToken to avoid Concurrency Exception on User Update
            _userRepository.AddRefreshToken(refreshTokenEntity);
            _unitOfWork.Commit();

            return new LoginResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = $"{user.FirstName} {user.LastName}",
                Role = user.Role?.Name.ToString() ?? "Unknown", // Role.Name is Enum RoleType
                Token = token,
                RefreshToken = refreshTokenRaw
            };
        }

        public UserProfileDto? GetProfile(Guid userId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null) return null;

            return new UserProfileDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Username = user.Username,
                Email = user.Email,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                PhoneNumber = user.PhoneNumber,
                Image = user.Image,
                StoreAddress = user.StoreAddress,
                Banner = user.Banner,
                StoreDescription = user.StoreDescription,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                Status = user.Status
            };
        }

        public bool UpdateProfile(Guid userId, UpdateProfileRequest request)
        {
            var user = _userRepository.GetById(userId);
            if (user == null) return false;

            // Update allowed fields
            if (request.FirstName != null) user.FirstName = request.FirstName;
            if (request.LastName != null) user.LastName = request.LastName;
            if (request.Gender != null) user.Gender = request.Gender;
            if (request.DateOfBirth != null) user.DateOfBirth = request.DateOfBirth;
            if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
            if (request.Image != null) user.Image = request.Image;
            
            // Artisan info updates
            if (request.StoreAddress != null) user.StoreAddress = request.StoreAddress;
            if (request.Banner != null) user.Banner = request.Banner;
            if (request.StoreDescription != null) user.StoreDescription = request.StoreDescription;

            _userRepository.Update(user);
            _unitOfWork.Commit();

            return true;
        }

        public bool Register(RegisterRequest request, out string errorMessage)
        {
            errorMessage = "";
            
            // Check if username or email exists
            if (_userRepository.GetByUsername(request.Username) != null)
            {
                errorMessage = "Username already exists";
                return false;
            }

            if (_userRepository.GetByEmail(request.Email) != null)
            {
                 errorMessage = "Email already exists";
                 return false;
            }
 

            var role = _userRepository.GetRoleByName(request.Role);
            if (role == null)
            {
                errorMessage = "Invalid Role";
                return false;
            }
            
            // Generate Verification Code
            var verificationCode = new Random().Next(100000, 999999).ToString();
            
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                HashedPassword = CreatePasswordHash(request.Password),
                Role = role,
                RoleId = role.Id,
                Status = AccountStatus.PendingVerification, // Pending Verification
                VerificationCode = verificationCode,
                VerificationCodeExpiryTime = DateTime.UtcNow.AddMinutes(15), 
                EmailConfirmed = false
            };

            _userRepository.Add(user);
            _unitOfWork.Commit();

            try 
            {
                _emailService.SendVerificationEmailAsync(user.Email, verificationCode).Wait();
            }
            catch(Exception ex)
            {
             
            }

            return true;
        }
        
        public bool VerifyEmail(VerifyEmailRequest request, out string errorMessage)
        {
            errorMessage = "";
            var user = _userRepository.GetByEmail(request.Email);
            if (user == null)
            {
                errorMessage = "User not found";
                return false;
            }
            
            if (user.EmailConfirmed)
            {
                errorMessage = "Email already verified";
                return false;
            }
            
            if (user.VerificationCode != request.Code)
            {
                errorMessage = "Invalid verification code";
                return false;
            }
            
            if (user.VerificationCodeExpiryTime < DateTime.UtcNow)
            {
                errorMessage = "Verification code expired";
                return false;
            }
            
            user.EmailConfirmed = true;
            user.Status = AccountStatus.Active;
            user.VerificationCode = null;
            user.VerificationCodeExpiryTime = null;
            
            _userRepository.Update(user);
            _unitOfWork.Commit();
            
            return true;
        }

        public bool ForgotPassword(ForgotPasswordRequest request, out string errorMessage)
        {
            errorMessage = "";
            var user = _userRepository.GetByEmail(request.Email);
            if (user == null)
            {
                errorMessage = "User not found";
                return false;
            }

            var resetCode = new Random().Next(100000, 999999).ToString();
            user.ResetPasswordToken = resetCode;
            //user.ResetPasswordTokenExpiryTime = DateTime.UtcNow.AddMinutes(15);
            
            _userRepository.Update(user);
            _unitOfWork.Commit();

            try
            {
                _emailService.SendPasswordResetEmailAsync(user.Email, resetCode).Wait();
            }
            catch(Exception ex)
            {
                errorMessage = "Failed to send email";
                return false;
            }

            return true;
        }

        public bool ResetPassword(ResetPasswordRequest request, out string errorMessage)
        {
            errorMessage = "";
            var user = _userRepository.GetByEmail(request.Email);
            if (user == null)
            {
                errorMessage = "User not found";
                return false;
            }

            if (user.ResetPasswordToken != request.Code)
            {
                errorMessage = "Invalid reset code";
                return false;
            }

            //if (user.ResetPasswordTokenExpiryTime < DateTime.UtcNow)
            //{
            //    errorMessage = "Reset code expired";
            //    return false;
            //}

            user.HashedPassword = CreatePasswordHash(request.NewPassword);
            user.ResetPasswordToken = null;
            //user.ResetPasswordTokenExpiryTime = null;

            _userRepository.Update(user);
            _unitOfWork.Commit();

            return true;
        }

        public bool ChangePassword(Guid userId, ChangePasswordRequest request, out string errorMessage)
        {
            errorMessage = "";
            var user = _userRepository.GetById(userId);
            if (user == null)
            {
                errorMessage = "User not found";
                return false;
            }

            if (!VerifyPasswordHash(request.OldPassword, user.HashedPassword))
            {
                errorMessage = "Incorrect old password";
                return false;
            }

            user.HashedPassword = CreatePasswordHash(request.NewPassword);
            
            _userRepository.Update(user);
            _unitOfWork.Commit();
            
            return true;
        }

        public Guid CreateUser(CreateUserRequest request, out string errorMessage)
        {
            errorMessage = "";
            if (_userRepository.GetByUsername(request.Username) != null)
            {
                errorMessage = "Username already exists";
                return Guid.Empty;
            }

            var role = _userRepository.GetRoleByName(request.Role);
            if (role == null)
            {
                errorMessage = "Invalid Role";
                return Guid.Empty;
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                HashedPassword = CreatePasswordHash(request.Password),
                Role = role,
                RoleId = role.Id,
                Status = request.Status,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                PhoneNumber = request.PhoneNumber
            };

            _userRepository.Add(user);
            _unitOfWork.Commit();

            return user.Id;
        }

        public bool AdminUpdateUser(Guid userId, UpdateUserAdminRequest request, out string errorMessage)
        {
            errorMessage = "";
            var user = _userRepository.GetById(userId);
            if (user == null)
            {
                errorMessage = "User not found";
                return false;
            }

            if (request.FirstName != null) user.FirstName = request.FirstName;
            if (request.LastName != null) user.LastName = request.LastName;
            if (request.Email != null) user.Email = request.Email;
            if (request.Status != null) user.Status = request.Status.Value;
            if (request.Gender != null) user.Gender = request.Gender;
            if (request.DateOfBirth != null) user.DateOfBirth = request.DateOfBirth;
            if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
            if (request.EmailConfirmed != null) user.EmailConfirmed = request.EmailConfirmed.Value;
            if (request.PhoneNumberConfirmed != null) user.PhoneNumberConfirmed = request.PhoneNumberConfirmed.Value;

            if (request.Role != null)
            {
                 var role = _userRepository.GetRoleByName(request.Role.Value);
                 if (role == null)
                 {
                     errorMessage = "Invalid Role";
                     return false;
                 }
                 user.Role = role;
                 user.RoleId = role.Id;
            }

            _userRepository.Update(user);
            _unitOfWork.Commit();

            return true;
        }

        public bool DeleteUser(Guid userId, out string errorMessage)
        {
            errorMessage = "";
            var user = _userRepository.GetById(userId);
            if (user == null)
            {
                errorMessage = "User not found";
                return false;
            }

            _userRepository.Delete(user);
            _unitOfWork.Commit();
            return true;
        }

        public bool BanUser(Guid userId, out string errorMessage)
        {
            errorMessage = "";
            var user = _userRepository.GetById(userId);
            if (user == null)
            {
                errorMessage = "User not found";
                return false;
            }

            user.Status = AccountStatus.Suspended;
            _userRepository.Update(user);
            _unitOfWork.Commit();
            return true;
        }

        public bool Logout(Guid userId, LogoutRequest request, out string errorMessage)
        {
            errorMessage = "";
            var user = _userRepository.GetUserWithRefreshTokens(userId);
            if (user == null)
            {
                errorMessage = "User not found";
                return false;
            }

            
            var tokenToRemove = user.RefreshTokens.FirstOrDefault(rt => 
            {
                 // Reconstruct hash check
                 // storedHash = rt.TokenHash (base64)
                 // salt = rt.TokenSalt (base64)
                 
                 // Reuse Verify logic but manually since VerifyPasswordHash expects "salt:hash" string
                 // or just reuse VerifyPasswordHash by constructing string "salt:hash"
                 
                 var storedFullHash = $"{rt.TokenSalt}:{rt.TokenHash}";
                 return VerifyPasswordHash(request.RefreshToken, storedFullHash);
            });

            if (tokenToRemove != null)
            {
                // NOTE: Remove the refresh token to logout
                user.RefreshTokens.Remove(tokenToRemove);
                _unitOfWork.Commit();
                return true;
            }
            
            errorMessage = "Invalid Token";
            return false;
        }

        public RefreshTokenResponse? RefreshToken(Guid userId, RefreshTokenRequest request, out string errorMessage)
        {
            errorMessage = "";
            var user = _userRepository.GetUserWithRefreshTokens(userId);
            if (user == null)
            {
                errorMessage = "User not found";
                return null;
            }

            // Verify existing token
            var tokenToRemove = user.RefreshTokens.FirstOrDefault(rt => 
            {
                 var storedFullHash = $"{rt.TokenSalt}:{rt.TokenHash}";
                 return VerifyPasswordHash(request.RefreshToken, storedFullHash);
            });

            if (tokenToRemove == null)
            {
                errorMessage = "Invalid Refresh Token";
                return null;
            }

            // Remove old token (Rotation)
            user.RefreshTokens.Remove(tokenToRemove);

            // Generate New Access Token
            var newToken = "mock-token-" + Guid.NewGuid(); 
            
            // Generate New Refresh Token
            var newRefreshTokenRaw = Guid.NewGuid().ToString();
            var newRefreshTokenHash = CreatePasswordHash(newRefreshTokenRaw);
            var newRefreshTokenParts = newRefreshTokenHash.Split(':');
            
            var newRefreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = newRefreshTokenParts[1],
                TokenSalt = newRefreshTokenParts[0],
                CreatedAt = DateTime.UtcNow
            };
            
            _userRepository.AddRefreshToken(newRefreshTokenEntity);             
            _unitOfWork.Commit();

            return new RefreshTokenResponse
            {
                Token = newToken,
                RefreshToken = newRefreshTokenRaw
            };
        }

        private bool VerifyPasswordHash(string password, string storedHash)
        {
            // Assuming storedHash is formatted as "Salt:Hash" in Base64      
           
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false; // Invalid format

            var salt = Convert.FromBase64String(parts[0]);
            var hash = Convert.FromBase64String(parts[1]);

            using (var hmac = new HMACSHA512(salt))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                for (int i = 0; i < computedHash.Length; i++)
                {
                    if (computedHash[i] != hash[i]) return false;
                }
            }
            return true;
        }

     
        private string CreatePasswordHash(string password)
        {
            using (var hmac = new HMACSHA512())
            {
                var salt = hmac.Key;
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
            }
        }

      
   
    }
}
