namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// Validates and applies the commands of one kind (the receiver in the Command pattern). It is
    /// built with the attempt it changes, so Execute takes only the command and allocates nothing.
    /// </summary>
    public interface ICommandHandler
    {
        CommandKind Kind { get; }

        /// <summary>Applies the command, or refuses it and changes nothing.</summary>
        CommandOutcome Execute(in GameCommand command);
    }
}
