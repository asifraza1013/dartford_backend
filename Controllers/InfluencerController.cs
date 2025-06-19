using dartford_api.Interfaces;
using dartford_api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dartford_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InfluencerController : ControllerBase
    {
        private readonly IInfluencerService _influencerService;

        public InfluencerController(IInfluencerService influencerService)
        {
            _influencerService = influencerService;
        }

        [HttpGet("getAllInfluencers")]
        public async Task<IActionResult> GetAllInfluencers()
        {
            var influencers = await _influencerService.GetAllInfluencers();
            return Ok(influencers);
        }

        [HttpGet("getInfluencerById/{id}")]
        public async Task<IActionResult> GetInfluencerById(int id)
        {
            var influencer = await _influencerService.GetInfluencerById(id);
            if (influencer == null)
                return NotFound("Influencer not found");

            return Ok(influencer);
        }

        [HttpPost("createNewInfluencer")]
        public async Task<IActionResult> CreateInfluencer([FromBody] Influencer influencer)
        {
            var created = await _influencerService.CreateInfluencer(influencer);
            return CreatedAtAction(nameof(GetInfluencerById), new { id = created.Id }, created);
        }

        [HttpPut("updateInfluencer/{id}")]
        public async Task<IActionResult> UpdateInfluencer(int id, [FromBody] Influencer influencer)
        {
            var updated = await _influencerService.UpdateInfluencer(id, influencer);
            if (!updated)
                return NotFound("Influencer not found");

            return NoContent();
        }

        [HttpDelete("deleteInfluencer/{id}")]
        public async Task<IActionResult> DeleteInfluencer(int id)
        {
            var deleted = await _influencerService.DeleteInfluencer(id);
            if (!deleted)
                return NotFound("Influencer not found");

            return NoContent();
        }
    }
}
