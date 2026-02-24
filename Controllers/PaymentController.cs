using Buffet_Restaurant_Managment_System_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly PromptPayService _promptPayService;
        public PaymentController(PromptPayService promptPayService)
        {
            _promptPayService = promptPayService;
        }
        [HttpPost("generate-qr")]
        public async Task<IActionResult> CreateQr([FromBody] decimal amount)
        {
            var result = await _promptPayService.GeneratePromptPayQr(amount);
            return Ok(result);
        }

        [HttpPost("check-status")]
        public async Task<IActionResult> CheckStatus([FromBody] string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId))
                return BadRequest("Transaction ID is required");

            var result = await _promptPayService.CheckPaymentStatus(transactionId);
            return Ok(result);
        }
    }
}