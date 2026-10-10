namespace WeihnachtsfeierBit.Data;

public class ShowProjektZuo
{
    public Guid Id { get; set; }
    public required Show Show { get; set; }
    public required Projekt Projekt { get; set; }
}
