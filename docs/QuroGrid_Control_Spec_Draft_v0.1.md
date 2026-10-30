# QuroGrid & QuroDataSet Control Spec (Draft v0.1)

Draft spec for the first QuroUI common components. Stack: Blazor Server (SignalR), ASP.NET Core, MudBlazor alongside. Screen and grid definitions in TOON format (successor to the legacy XML screen definitions). All visible text supports multiple languages.

## 1. Design Principle

- **QuroDataSet holds the data. QuroGrid only shows it.** The grid binds to a dataset; it never owns data.
- One dataset can be bound to many controls (grid, text box, combo box, etc.). A change in the dataset updates every bound control.
- Grid and dataset are separate components in the QuroGrid project (/WRK/QuroGrid). QuroERP imports them.

## 2. QuroDataSet

### 2.1 Structure

- **Columns**: name, data type (string, int, decimal, date, datetime, bool), key flag, required flag, default value, max length, decimal places.
- **Rows**: each row keeps current values, original values, and a row state.
- **Row state**: Unchanged, Inserted, Updated, Deleted.
- **Current row**: one row position shared by all bound controls.

### 2.2 Operations

- Load from REST API (JSON) and clear.
- Add row, insert row at position, delete row (marks Deleted, does not remove until save).
- Get / set cell value by row and column.
- Undo changes on one row or all rows (restore original values).
- Get changed rows only (Inserted, Updated, Deleted) for saving.
- Accept changes after a successful save (all rows become Unchanged).
- Filter and sort (client side), without changing original order.
- Sum, count, average, min, max per column.

### 2.3 Validation

- Required, max length, data type, number range.
- Validation runs before save; errors return row and column so the grid can highlight the cell.

### 2.4 Events

- RowChanged (current row moved), ValueChanged (cell edited), RowAdded, RowDeleted, Loaded, Saved.

## 3. QuroGrid

### 3.1 Display

- Column header with multi-language caption, width, alignment (horizontal: left, center, right; vertical: top, middle, bottom), visible/hidden, frozen (fixed) columns on the left and frozen rows at the top.
- Multi-row header (merged header) for grouped columns, and multi-row records: one record can be shown over two or more lines in the body (Nexacro style).
- Row number column and row state indicator (new, edited, deleted).
- Number and date formatting per column; thousand separators.
- Summary band (footer row) for grand totals (sum, count, etc.) using dataset functions, plus group subtotal rows (for example, a subtotal per customer). Column property "suppress" (Nexacro style): consecutive same values down a column are merged into one cell.
- Virtual scrolling for large data (v1 scope: virtual scroll only).

### 3.2 Editing

- One column = one control type (1:1); one control per cell. Following the Nexacro Grid model, each column has two settings: displaytype (how the cell looks) and edittype (how it is edited). Types: text box, static label, combo box, date picker (calendar), mask, check box, image, read-only. Combo box lists come from the common code table: a separate combo dataset loaded once when the screen opens, bound by combo dataset + code column + name column (Nexacro style).
- Keyboard: arrow keys, Tab, Enter to move; F2 or typing to edit; Esc to cancel.
- Add row, delete row, copy and paste (Excel-compatible).
- Required and invalid cells are highlighted; error message shown as tooltip.
- Cell-level read-only rule (for example, key columns read-only after save).

### 3.3 Other Features

- Column sort by header click, column filter.
- Column resize and reorder; user layout saved per user.
- Row selection: single, multiple, check box column.
- Export to Excel.
- Cell style rules (color by value), e.g. negative numbers in red.

### 3.4 Events

- CellClick, CellDoubleClick, CellEditStart, CellEditEnd, RowSelected, BeforeRowDelete (can cancel).

## 4. TOON Definition (Example Shape)

Screen developers define the dataset and grid in TOON, not in code. Example fields:

- dataset: id, api, columns (name, type, key, required, length)
- grid: id, dataset, columns (field, captionKey, width, align, editType, format, frozen, visible)

The caption uses a language key (captionKey), resolved by the multi-language service at runtime.

## 5. Multi-language

- All captions, messages, and tooltips use language keys.
- Date and number formats follow the user's locale.

## 6. Decisions (v1)

- Decided: the dataset is given the REST API URL and calls the ASP.NET Core API itself (HttpClient on the Blazor Server side).
- Decided: tree grid (hierarchy) is included in v1.
- Decided: v1 = virtual scrolling only. Server-side paging is deferred to the next version.
- Decided: save sends only changed rows (inserted, updated, deleted) together as one batch.

## 7. Open Questions for Cho (based on Nexacro Grid features)

1. Decided: select type is a grid option — row, cell, multiple rows, or area (like Excel).
2. Decided: cell expressions are supported — a cell can show a calculated value, like quantity × price.
3. Decided: conditional style is supported — cell color or font changes by value, set by an expression (e.g. red for negative).
4. Decided: header check box is supported — selects or clears all rows.
5. Decided: follow Nexacro — word wrap is a cell property, and auto row height (autosizing) is a grid option.
6. Decided: multiple formats are supported — one grid can hold several layouts and the screen can switch between them (like Nexacro formats).
7. Deferred: body cell merge (fixed colspan / rowspan) moves to the next version, not v1.
8. Decided: auto enter — editing starts as soon as a cell is selected.
9. Decided: Excel export only — no import in v1.
10. Decided: no no-data message — the grid stays empty when the dataset is empty.
11. Decided: tooltip — when cell text is cut off, hovering shows the full text.
12. Deferred: drag-and-drop row reordering is not in v1, saved for later.

## 8. QuroUI Text Box (Decisions v1)

- Follows the Nexacro **Edit** component concept.
- Binds to a dataset column (Nexacro bind style); its value saves with the dataset's batch save.
- Input limits: allowed character type (e.g. numbers only) and maximum length.
- Password mode: typed characters are masked.
- Placeholder: light grey hint text when empty (multi-language).
- Required field: shown with a **light yellow background** (no red star); saving is blocked when a required field is empty.
- Auto select: all text is selected when the box gets focus.
- Read-only: shown with a grey background.

## 9. QuroUI Combo Box (Decisions v1)

- Follows the Nexacro **Combo** component concept; list comes from the common code table (same as grid combos).
- Type to filter the list.
- Empty choice at the top (e.g. "All" or "Select").
- Linked combos: the choice in a parent combo changes the child combo's list (e.g. country → city).
- Multi-select with check boxes.
- Show code and name together (e.g. "01 Seoul") — option.
- Multi-column dropdown: the open list can show extra columns besides code and name.

* No server-side loading while typing: the whole list loads at once.

## 10. QuroUI Date Picker (Core Spec v1)

- Follows the Nexacro **Calendar** component concept (core features only).
- Date range selection (from date \~ to date).
- Month mode: pick year and month only (e.g. for month-end closing).
- Direct typing without opening the calendar.
- Date format follows the user's language (Korean: year-month-day, English: month-day-year).

## 11. QuroUI Static Text (Core Spec v1, in progress)

- Shows a multi-language label.
- Can bind to a dataset column and show its value.
- Next: finish static text, then the mask component.
