using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using WMS_HU_Model.Map.Dtos;

namespace WMS_HU_Model.Map.Import
{
    public class ExcelSourceRect
    {
        public string SheetName { get; set; }
        public int StartRow { get; set; }
        public int EndRow { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public string RawText { get; set; }
        public bool IsMerged { get; set; }
        public string Reference { get { return SheetName + "!R" + (StartRow + 1) + "C" + (StartColumn + 1); } }
    }

    public class MapImportCandidate
    {
        public ExcelSourceRect Source { get; set; }
        public string Text { get; set; }
        public string Expression { get; set; }
        public List<string> LocationCodes { get; set; }
        public decimal X { get { return Source.X; } }
        public decimal Y { get { return Source.Y; } }
        public decimal SizeX { get { return Source.Width; } }
        public decimal SizeY { get { return Source.Height; } }
        public bool DirectionUncertain { get; set; }
        public bool IsSharedSourceCell { get; set; }
    }

    public class MapExcelParseResult
    {
        public List<MapImportCandidate> Candidates { get; set; }
        public List<MapValidationIssueDto> Issues { get; set; }
        public decimal MinX { get; set; }
        public decimal MinY { get; set; }
        public decimal MaxX { get; set; }
        public decimal MaxY { get; set; }
    }

    public class MapExcelImportService
    {
        public const decimal BoundsMargin = 20m;
        public const decimal HorizontalAspect = 1.5m;
        public const decimal VerticalAspect = 0.6666667m;

        public MapExcelParseResult Parse(Stream stream, string fileName)
        {
            if (stream == null || !stream.CanRead) throw new InvalidOperationException("Excel 文件流不可读。");
            List<MapImportCandidate> candidates = new List<MapImportCandidate>();
            List<MapValidationIssueDto> issues = new List<MapValidationIssueDto>();
            List<ExcelSourceRect> usedRects = new List<ExcelSourceRect>();
            IWorkbook workbook = WorkbookFactory.Create(stream);
            try
            {
                for (int sheetIndex = 0; sheetIndex < workbook.NumberOfSheets; sheetIndex++)
                {
                    ISheet sheet = workbook.GetSheetAt(sheetIndex);
                    List<ExcelSourceRect> sources = ExtractSourceRects(sheet);
                    usedRects.AddRange(sources);
                    foreach (ExcelSourceRect source in sources.Where(x => !string.IsNullOrWhiteSpace(x.RawText)))
                    {
                        string expression = MapRangeExpander.ExtractExpression(source.RawText);
                        List<string> codes = expression == null ? new List<string>() : MapRangeExpander.Expand(expression);
                        if (codes.Count == 0) continue;
                        decimal aspect = source.Height <= 0 ? decimal.MaxValue : source.Width / source.Height;
                        bool uncertain = codes.Count > 1 && aspect > VerticalAspect && aspect < HorizontalAspect;
                        bool shared = codes.Count > 1 && expression.IndexOf('/') >= 0;
                        candidates.Add(new MapImportCandidate { Source = source, Text = source.RawText, Expression = expression, LocationCodes = codes, DirectionUncertain = uncertain, IsSharedSourceCell = shared });
                        if (uncertain) issues.Add(Issue("WARNING", "IMPORT_SPLIT_DIRECTION_AMBIGUOUS", expression, "来源区域方向不明确，已按水平方向等分：" + source.Reference));
                        if (shared) issues.Add(Issue("INFO", "SHARED_SOURCE_CELL_SPLIT", expression, "同一来源单元格包含多个货位，已自动等分：" + source.Reference));
                    }
                }
            }
            finally
            {
                workbook.Close();
            }
            if (candidates.Count == 0) issues.Add(Issue("WARNING", "NO_LOCATION_CANDIDATE", null, "工作簿中未识别到货位编码或范围。"));
            return CreateResult(candidates, issues, usedRects);
        }

        public static List<ExcelSourceRect> ExtractSourceRects(ISheet sheet)
        {
            List<ExcelSourceRect> result = new List<ExcelSourceRect>();
            if (sheet == null) return result;
            int maxColumn = 0;
            int maxRow = sheet.LastRowNum;
            for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                IRow row = sheet.GetRow(rowIndex);
                if (row != null && row.LastCellNum > maxColumn) maxColumn = row.LastCellNum;
            }
            for (int index = 0; index < sheet.NumMergedRegions; index++)
            {
                CellRangeAddress merged = sheet.GetMergedRegion(index);
                maxColumn = Math.Max(maxColumn, merged.LastColumn + 1);
                maxRow = Math.Max(maxRow, merged.LastRow);
            }
            if (maxColumn == 0 && sheet.NumMergedRegions == 0) return result;

            decimal[] columnOffsets = new decimal[maxColumn + 1];
            for (int column = 0; column < maxColumn; column++) columnOffsets[column + 1] = columnOffsets[column] + Convert.ToDecimal(sheet.GetColumnWidthInPixels(column));
            decimal[] rowOffsets = new decimal[maxRow + 2];
            for (int row = 0; row <= maxRow; row++) rowOffsets[row + 1] = rowOffsets[row] + RowHeight(sheet, row);

            HashSet<string> mergedCells = new HashSet<string>();
            for (int index = 0; index < sheet.NumMergedRegions; index++)
            {
                CellRangeAddress range = sheet.GetMergedRegion(index);
                result.Add(Rect(sheet, range.FirstRow, range.LastRow, range.FirstColumn, range.LastColumn, columnOffsets, rowOffsets, true));
                for (int row = range.FirstRow; row <= range.LastRow; row++)
                    for (int column = range.FirstColumn; column <= range.LastColumn; column++) mergedCells.Add(row + ":" + column);
            }
            for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                IRow row = sheet.GetRow(rowIndex);
                if (row == null || row.LastCellNum < 0) continue;
                for (int column = Math.Max(0, Convert.ToInt32(row.FirstCellNum)); column < row.LastCellNum; column++)
                {
                    if (mergedCells.Contains(rowIndex + ":" + column)) continue;
                    ICell cell = row.GetCell(column);
                    if (cell != null) result.Add(Rect(sheet, rowIndex, rowIndex, column, column, columnOffsets, rowOffsets, false));
                }
            }
            return result;
        }

        public static List<ExcelSourceRect> Split(ExcelSourceRect source, int count)
        {
            List<ExcelSourceRect> result = new List<ExcelSourceRect>();
            if (source == null || count <= 0) return result;
            decimal aspect = source.Height <= 0 ? decimal.MaxValue : source.Width / source.Height;
            bool horizontal = aspect > VerticalAspect;
            for (int index = 0; index < count; index++)
            {
                decimal x = horizontal ? source.X + source.Width * index / count : source.X;
                decimal y = horizontal ? source.Y : source.Y + source.Height * index / count;
                decimal width = horizontal ? (index == count - 1 ? source.X + source.Width - x : source.Width / count) : source.Width;
                decimal height = horizontal ? source.Height : (index == count - 1 ? source.Y + source.Height - y : source.Height / count);
                result.Add(new ExcelSourceRect { SheetName = source.SheetName, StartRow = source.StartRow, EndRow = source.EndRow, StartColumn = source.StartColumn, EndColumn = source.EndColumn, X = x, Y = y, Width = width, Height = height, RawText = source.RawText, IsMerged = source.IsMerged });
            }
            return result;
        }

        public static decimal RowHeightToLogical(decimal points) { return points * 4m / 3m; }

        private static decimal RowHeight(ISheet sheet, int rowIndex)
        {
            IRow row = sheet.GetRow(rowIndex);
            decimal points = row == null || row.HeightInPoints <= 0 ? Convert.ToDecimal(sheet.DefaultRowHeightInPoints) : Convert.ToDecimal(row.HeightInPoints);
            return RowHeightToLogical(points);
        }

        private static ExcelSourceRect Rect(ISheet sheet, int firstRow, int lastRow, int firstColumn, int lastColumn, decimal[] columnOffsets, decimal[] rowOffsets, bool merged)
        {
            IRow row = sheet.GetRow(firstRow);
            ICell cell = row == null ? null : row.GetCell(firstColumn);
            return new ExcelSourceRect
            {
                SheetName = sheet.SheetName, StartRow = firstRow, EndRow = lastRow, StartColumn = firstColumn, EndColumn = lastColumn,
                X = columnOffsets[firstColumn], Y = rowOffsets[firstRow], Width = columnOffsets[lastColumn + 1] - columnOffsets[firstColumn],
                Height = rowOffsets[lastRow + 1] - rowOffsets[firstRow], RawText = CellText(cell), IsMerged = merged
            };
        }

        private static MapExcelParseResult CreateResult(List<MapImportCandidate> candidates, List<MapValidationIssueDto> issues, List<ExcelSourceRect> rects)
        {
            if (rects.Count == 0) return new MapExcelParseResult { Candidates = candidates, Issues = issues, MinX = 0, MinY = 0, MaxX = 1200, MaxY = 800 };
            return new MapExcelParseResult
            {
                Candidates = candidates, Issues = issues,
                MinX = rects.Min(x => x.X) - BoundsMargin, MinY = rects.Min(x => x.Y) - BoundsMargin,
                MaxX = rects.Max(x => x.X + x.Width) + BoundsMargin, MaxY = rects.Max(x => x.Y + x.Height) + BoundsMargin
            };
        }

        private static string CellText(ICell cell)
        {
            if (cell == null) return string.Empty;
            if (cell.CellType == CellType.Formula)
            {
                if (cell.CachedFormulaResultType == CellType.Numeric) return cell.NumericCellValue.ToString();
                return cell.StringCellValue == null ? string.Empty : cell.StringCellValue.Trim();
            }
            return cell.ToString().Trim();
        }

        private static MapValidationIssueDto Issue(string severity, string code, string bindingCode, string message)
        {
            return new MapValidationIssueDto { Severity = severity, Code = code, BindingCode = bindingCode, Message = message };
        }
    }
}
