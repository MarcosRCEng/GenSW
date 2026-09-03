namespace GenSW.Application.Breeds;

public sealed class RacaDuplicateException : Exception
{
    public RacaDuplicateException() : base("A breed with the same name already exists for this species.") { }
    public RacaDuplicateException(Exception innerException) : base("A breed with the same name already exists for this species.", innerException) { }
}
