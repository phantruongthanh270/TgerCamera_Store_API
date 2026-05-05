using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace TgerCamera.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AddressController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AddressController> _logger;
        private const string AddressKitApiBase = "https://production.cas.so/address-kit/2025-07-01";

        public AddressController(IHttpClientFactory httpClientFactory, ILogger<AddressController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Lấy toàn bộ provinces (Tỉnh/Thành phố)
        /// </summary>
        [HttpGet("provinces")]
        public async Task<IActionResult> GetProvinces()
        {
            try
            {
                _logger.LogInformation("Fetching provinces from {Url}", $"{AddressKitApiBase}/provinces");

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync($"{AddressKitApiBase}/provinces");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Provinces response received: {Length} bytes", content.Length);

                    // Trả raw JSON string trực tiếp với content type phù hợp
                    return Content(content, "application/json");
                }

                _logger.LogError("Failed to fetch provinces. Status: {Status}", response.StatusCode);
                return StatusCode((int)response.StatusCode, new { message = "Failed to fetch provinces data" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching provinces: {Message}", ex.Message);
                return StatusCode(500, new { message = "Error fetching provinces", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy toàn bộ communes/wards (Xã/Phường) theo province ID
        /// </summary>
        [HttpGet("communes/{provinceId}")]
        public async Task<IActionResult> GetCommunes(string provinceId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(provinceId))
                {
                    _logger.LogWarning("GetCommunes called with empty provinceId");
                    return BadRequest(new { message = "Province ID is required" });
                }

                var url = $"{AddressKitApiBase}/provinces/{provinceId}/communes";
                _logger.LogInformation("Fetching communes from {Url}", url);

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Communes response received for province {ProvinceId}: {Length} bytes", provinceId, content.Length);

                    // Trả raw JSON string trực tiếp với content type phù hợp
                    return Content(content, "application/json");
                }

                _logger.LogError("Failed to fetch communes for province {ProvinceId}. Status: {Status}", provinceId, response.StatusCode);
                return StatusCode((int)response.StatusCode, new { message = "Failed to fetch communes data" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching communes for province {ProvinceId}: {Message}", provinceId, ex.Message);
                return StatusCode(500, new { message = "Error fetching communes", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy toàn bộ districts (Quận/Huyện) - Deprecated, hãy dùng GetCommunes thay thế
        /// </summary>
        [HttpGet("districts")]
        public async Task<IActionResult> GetDistricts()
        {
            try
            {
                var url = $"{AddressKitApiBase}/provinces";
                _logger.LogInformation("Fetching all data from {Url}", url);

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Districts data received: {Length} bytes", content.Length);

                    // Trả raw JSON string trực tiếp với content type phù hợp
                    return Content(content, "application/json");
                }

                _logger.LogError("Failed to fetch districts. Status: {Status}", response.StatusCode);
                return StatusCode((int)response.StatusCode, new { message = "Failed to fetch districts data" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching districts: {Message}", ex.Message);
                return StatusCode(500, new { message = "Error fetching districts", error = ex.Message });
            }
        }
    }
}
