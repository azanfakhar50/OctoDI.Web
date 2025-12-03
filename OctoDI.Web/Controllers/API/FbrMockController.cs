using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace OctoDI.Web.Controllers.Api
{
    [ApiController]
    [Produces("application/json")] 
    [Consumes("application/json")] 
    [Route("api/fbrmock")]
    public class FbrMockController : ControllerBase
    {
        // ==================== VALIDATE INVOICE (Sandbox) ====================
        [HttpPost("validateinvoicedata_sb")]
        public IActionResult ValidateInvoice([FromBody] JObject invoice)
        {
            try
            {
                var sellerId = invoice["sellerNTN"] ?? invoice["sellerCNIC"];
                var buyerId = invoice["buyerNTN"] ?? invoice["buyerCNIC"];

                if (sellerId == null || buyerId == null)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Missing required seller or buyer NTN/CNIC.",
                        ErrorCode = "MOCK_ERR_400",
                        Timestamp = DateTime.Now
                    });
                }


                // Mock success response
                var response = new
                {
                    Success = true,
                    Message = "Invoice validated successfully in FBR Mock Sandbox",
                    InvoiceNumber = "SB-" + new Random().Next(1000, 9999),
                    ValidationDate = DateTime.Now,
                    Remarks = "Mock Environment — No record created at FBR"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal mock API error: " + ex.Message
                });
            }
        }

        // ==================== POST INVOICE (Mock Production Simulation) ====================
        [HttpPost("postinvoicedata")]
        public IActionResult PostInvoice([FromBody] JObject invoice)
        {
            try
            {
                // 🔹 Validate seller and buyer info
                if (invoice["sellerNTNCNIC"] == null || invoice["buyerNTNCNIC"] == null)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Missing required seller or buyer NTN/CNIC.",
                        ErrorCode = "MOCK_ERR_400",
                        Timestamp = DateTime.Now
                    });
                }

                // 🔹 Extract sample data for response
                string seller = invoice["sellerBusinessName"]?.ToString() ?? "Unknown Seller";
                string buyer = invoice["buyerBusinessName"]?.ToString() ?? "Unknown Buyer";
                var items = invoice["items"]?.ToObject<JArray>();

                decimal totalExclTax = 0;
                decimal totalTax = 0;

                if (items != null)
                {
                    foreach (var item in items)
                    {
                        totalExclTax += item["valueSalesExcludingST"]?.Value<decimal>() ?? 0;
                        totalTax += item["salesTaxApplicable"]?.Value<decimal>() ?? 0;
                    }
                }

                // 🔹 Generate mock invoice number
                //string mockFbrInvoiceNo = "INV-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(10000, 99999);
                string companyCode = "OCT";
                string mockFbrInvoiceNo = $"FBR{companyCode}{DateTime.Now:yyMMdd}{new Random().Next(100, 9999)}";

                // ✅ Build clean mock response (NO QR, NO IRN)
                var response = new
                {
                    Success = true,
                    Message = "Invoice successfully posted to Mock FBR Server.",
                    FBRInvoiceNo = mockFbrInvoiceNo,
                    Seller = seller,
                    Buyer = buyer,
                    TotalExclTax = totalExclTax,
                    TotalTax = totalTax,
                    GrandTotal = totalExclTax + totalTax,
                    PostingDate = DateTime.Now,
                    Note = "Mock environment - no real submission to FBR"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal mock API error: " + ex.Message
                });
            }
        }
    }
}
