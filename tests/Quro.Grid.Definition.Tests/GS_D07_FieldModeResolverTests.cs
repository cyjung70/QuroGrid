namespace Quro.Grid.Definition.Tests;

// GS-D07: mode × FormState × read-only permission (design v0.2 5.2, harness H6).
public class GS_D07_FieldModeResolverTests
{
    // Design 5.2 table: New, Loaded. E = edit, ER = edit + required, R = read, H = hidden.
    // Locked or a read-only permission turns every visible field into R.
    private static readonly Dictionary<FieldMode, (string New, string Loaded)> Table = new()
    {
        [FieldMode.Opt] = ("E", "E"),
        [FieldMode.Req] = ("ER", "ER"),
        [FieldMode.Key] = ("ER", "R"),
        [FieldMode.NewOnly] = ("E", "R"),
        [FieldMode.Ro] = ("R", "R"),
        [FieldMode.Auto] = ("R", "R"),
        [FieldMode.Hidden] = ("H", "H"),
    };

    public static TheoryData<FieldMode, FormState, bool, string> Matrix()
    {
        var data = new TheoryData<FieldMode, FormState, bool, string>();
        foreach (var (mode, row) in Table)
            foreach (var state in Enum.GetValues<FormState>())
                foreach (var permReadOnly in new[] { false, true })
                {
                    var expected = state switch { FormState.New => row.New, FormState.Loaded => row.Loaded, _ => "R" };
                    if (permReadOnly || state == FormState.Locked)
                        expected = row.New == "H" ? "H" : "R";
                    data.Add(mode, state, permReadOnly, expected);
                }
        return data;
    }

    /// <summary>GS-D07: all 42 combinations match the design table</summary>
    [Theory]
    [MemberData(nameof(Matrix))]
    public void GS_D07_ModeStateMatrix(FieldMode mode, FormState state, bool permReadOnly, string expected)
    {
        var s = FieldModeResolver.Resolve(mode, state, permReadOnly);

        var actual = (s.Visible, s.Editable, s.Required) switch
        {
            (false, false, false) => "H",
            (true, false, false) => "R",
            (true, true, false) => "E",
            (true, true, true) => "ER",
            _ => $"invalid {s}",
        };
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GS_D07_MatrixHas42Cases() => Assert.Equal(42, Matrix().Count);

    /// <summary>GS-D07: grid columns use the same resolver — edit=new edits only on added rows (state New)</summary>
    [Theory]
    [InlineData(ColumnEdit.NewRowOnly, false, FormState.New, true, false)]
    [InlineData(ColumnEdit.NewRowOnly, false, FormState.Loaded, false, false)]
    [InlineData(ColumnEdit.Editable, true, FormState.Loaded, true, true)]
    [InlineData(ColumnEdit.Editable, false, FormState.Loaded, true, false)]
    [InlineData(ColumnEdit.ReadOnly, false, FormState.New, false, false)]
    public void GS_D07_ColumnEditMapsToMode(ColumnEdit edit, bool req, FormState rowState, bool editable, bool required)
    {
        var s = FieldModeResolver.Resolve(FieldModeResolver.ModeOf(edit, req), rowState, permReadOnly: false);

        Assert.Equal((true, editable, required), (s.Visible, s.Editable, s.Required));
    }
}
