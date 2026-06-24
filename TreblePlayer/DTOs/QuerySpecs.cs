namespace TreblePlayer.DTOs;


public enum SortDirection
{
    Ascending,
    Descending
}

public record SortSpecification
{
    public string Field { get; set; }
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}

public record GroupingSpecification
{
    
}
public record FilterSpecification
{
    public string Field { get; set; }
    public string Value { get; set; }
}
