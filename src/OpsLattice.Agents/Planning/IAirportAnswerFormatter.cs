namespace OpsLattice.Agents.Planning;

public interface IAirportAnswerFormatter
{
    string Format(
        string question,
        AirportQueryExecutionResult executionResult);
}
