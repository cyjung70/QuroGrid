# QuroGrid

Data-entry grid and input controls for **Blazor Server** (.NET 10), built for ERP screens.
No dependency on a UI component library. MIT license.

| Package | Contents |
|---|---|
| `Quro.Grid.Data` | `QuroDataSet<T>`: row states, original values, validation, change set for saving. UI-free |
| `Quro.Grid.Definition` | Screen definition (TOON): parser, validator and immutable model. UI-free |
| `Quro.Grid` | `QuroGrid` and `QuroColumn` Razor components: editing, keyboard, Excel paste, filter, sort, grouping, footer sums |
| `Quro.UI` | Input controls bound to a data set column: text, mask, combo, date, label, static text |
| `Quro.Grid.DefLint` | .NET tool `quro-deflint`: checks screen definition files in CI |

These are pre-release (`0.x-alpha`) packages; the public API may still change between versions.
See `CHANGELOG.md` in the repository for changes.

## Setup

```html
<link rel="stylesheet" href="_content/Quro.Grid/quro-grid.css" />
<link rel="stylesheet" href="_content/Quro.UI/quro-ui.css" />
<script src="_content/Quro.Grid/quro-grid.js"></script>
```

```razor
<QuroGrid DataSet="@lines">
    <QuroColumn Field="ItemCode" HeaderKey="item.code" Type="CellType.Popup" />
    <QuroColumn Field="Qty" HeaderKey="item.qty" Type="CellType.Number" Format="N0" Aggregate="Aggregate.Sum" />
</QuroGrid>
```

Header texts are resource keys resolved by the app's localizer. There is no service registration;
data and behavior are passed as component parameters.

## Links

- Source and documentation: https://github.com/cyjung70/QuroGrid
- Public API contract: `docs/integration/consumer-contract.md` in the repository
