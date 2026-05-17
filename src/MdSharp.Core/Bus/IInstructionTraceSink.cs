namespace MdSharp.Core.Bus;

public interface IInstructionTraceSink
{
    uint CurrentM68kPc { get; set; }
}
