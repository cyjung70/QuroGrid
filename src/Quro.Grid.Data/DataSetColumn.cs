namespace Quro.Grid.Data;

/// <summary>
/// Column metadata of a data set (Addendum A-52). Built from <c>T</c> and supplemented by the screen definition
/// (<see cref="QuroDataSet{T}.SetColumn"/>). Descriptive: <see cref="QuroDataSet{T}.Validate"/> still uses
/// <see cref="FieldRule"/> only.
/// </summary>
/// <param name="Name">Property name of <c>T</c>.</param>
/// <param name="Type">Property type, including nullable (<c>DateOnly?</c>).</param>
/// <param name="Key">Part of the row key (<c>[Key]</c>).</param>
/// <param name="Required">Value is required (<c>[Required]</c>).</param>
/// <param name="Default">Value filled when a row is added without a template (<c>[DefaultValue]</c>).</param>
/// <param name="MaxLength">Largest text length (<c>[MaxLength]</c>, <c>[StringLength]</c>).</param>
/// <param name="Scale">Decimal places. Not taken from <c>T</c>.</param>
public sealed record DataSetColumn(
    string Name,
    Type Type,
    bool Key = false,
    bool Required = false,
    object? Default = null,
    int? MaxLength = null,
    int? Scale = null);
