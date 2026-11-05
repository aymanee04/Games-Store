using GameStore.Api.AI.services;
using GameStore.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GameStore.Api.AI.Controllers;

[ApiController]
[Route("api/assistant/games")]
[Authorize(Roles = "Admin")]
public class GameAssistantController(
    IGameAssistantService assistant) : ControllerBase
{
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GameGenerationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GameGenerationDto>> Generate(
        GenerateGameRequest request,
        CancellationToken ct)
    {
        var result = await assistant.GenerateGameAsync(
            request.Prompt,
            ct);

        return Ok(result);
    }
    
    [HttpPost("create")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GameDto>> Create(
        GameGenerationDto draft,
        CancellationToken ct)
    {
        var created = await assistant.CreateGameAsync(draft, ct);

        return CreatedAtAction(
            nameof(GamesController.GetById),
            "Games",
            new { id = created.Id },
            created);
    }
}