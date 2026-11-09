using GameStore.Api.AI.DTOs;

namespace GameStore.Api.AI.Interfaces;

public interface ICsvGameParser
{
    List<ExtractedGameDto> Parse(string content);
}