using System.Net;
using System.Threading;
using System.Threading.Tasks;
using API.Dtos;
using Application.Errors;
using Core.Entities;
using Core.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace API.CQRS
{
    public class GoogleLogin
    {
        public class Query : IRequest<UserDto>
        {
            public string tokenId { get; set; }
        }

        public class Handler : IRequestHandler<Query, UserDto>
        {
            private readonly UserManager<AppUser> _userManager;
            private readonly IGoogleAccessor _googleAccessor;
            private readonly IJwtGenerator _jwtGenerator;
            private readonly ILogger<Handler> _logger;
            public Handler(UserManager<AppUser> userManager, IGoogleAccessor googleAccessor, IJwtGenerator jwtGenerator, ILogger<Handler> logger)
            {
                _logger = logger;
                _jwtGenerator = jwtGenerator;
                _googleAccessor = googleAccessor;
                _userManager = userManager;
            }

            public async Task<UserDto> Handle(Query request, CancellationToken cancellationToken)
            {
                _logger.LogInformation($"THE TOKEN ID IS: {request.tokenId}");

                var userInfo = _googleAccessor.GoogleLogin(request.tokenId);

                if (userInfo == null) throw new RestException(HttpStatusCode.BadRequest, "Problem validating token.");

                var user = await _userManager.FindByEmailAsync(userInfo.Email);
                var roles = await _userManager.GetRolesAsync(user);

                if (user == null)
                {
                    user = new AppUser
                    {
                        DisplayName = userInfo.Name,
                        Id = userInfo.Id,
                        Email = userInfo.Email,
                        UserName = "g_" + userInfo.Id
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded) throw new RestException(HttpStatusCode.BadRequest, new { User = "Problem creating user." });
                }

                return new UserDto
                {
                    DisplayName = user.DisplayName,
                    Token = _jwtGenerator.CreateToken(user, roles),
                    Username = user.UserName,
                    Roles = roles
                };

            }
        }
    }
}