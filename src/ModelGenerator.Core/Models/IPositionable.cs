namespace ModelGenerator.Core.Models;

/// <summary>An item that can be placed on the shape's surface via AutoCenter/Manual/Relative
/// positioning — implemented by both TextLine and SvgInsert so ITextPositioner and
/// ModelOrchestrator can position either without duplicating dispatch logic.</summary>
public interface IPositionable
{
    TextPositionMode PositionMode { get; }
    float PositionX { get; }
    float PositionY { get; }
    float PositionZ { get; }
    float RotationZ { get; }
}
