using System.Collections.Generic;
using System.Text;

namespace Gear.Utilities.UI.ListView
{
    /// <summary>轻量 CSV 解析：支持逗号/自定义分隔符、引号字段、双引号转义与字段内换行。</summary>
    public static class GearListCsv
    {
        public static List<GearListRowData> Parse(string csv, char delimiter = ',', bool hasHeader = false)
        {
            List<GearListRowData> rows = new List<GearListRowData>();
            if (string.IsNullOrEmpty(csv)) return rows;

            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    fields.Add(field.ToString());
                    field.Clear();
                    AddRow(rows, fields);
                    fields.Clear();
                }
                else
                {
                    field.Append(c);
                }
            }

            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                AddRow(rows, fields);
            }

            if (hasHeader && rows.Count > 0) rows.RemoveAt(0);
            return rows;
        }

        private static void AddRow(List<GearListRowData> rows, List<string> fields)
        {
            if (fields.Count == 1 && string.IsNullOrEmpty(fields[0])) return;
            rows.Add(new GearListRowData(fields.ToArray()));
        }
    }
}
