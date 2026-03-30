using TgerCamera.Models;

namespace TgerCamera.Services;

public interface ITokenService
{
    string CreateToken(User user);
}
