namespace WeihnachtsfeierBit.Data;

public class ShowProjektZuo
{
    public Guid Id { get; set; }
    public Guid ShowId { get; set; }
    public required Show Show { get; set; }
    public Guid ProjektId { get; set; }
    public required Projekt Projekt { get; set; }
}
