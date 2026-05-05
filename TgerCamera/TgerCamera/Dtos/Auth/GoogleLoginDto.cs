using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TgerCamera.Dtos.Auth;

public class GoogleLoginDto
{
    [JsonPropertyName("idToken")]
    [Required(ErrorMessage = "IdToken is required")]
    public string? IdToken { get; set; }

    [JsonPropertyName("id_token")]
    public string? AlternateIdToken
    {
        set
        {
            if (string.IsNullOrWhiteSpace(IdToken))
            {
                IdToken = value;
            }
        }
    }
}
