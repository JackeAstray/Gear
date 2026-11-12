using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Evo.UI
{
    public partial class ListView
    {
        /// <summary>
        /// Imports columns and rows from CSV text.
        /// </summary>
        public void ImportFromCSV(string csvText, bool hasHeaders = true, bool clearExisting = true)
        {
            if (string.IsNullOrEmpty(csvText))
                return;

            var parsedData = ParseCSV(csvText);
            if (parsedData.Count == 0)
                return;

            if (clearExisting)
            {
                columns.Clear();
                rows.Clear();
            }

            int startRow = 0;
            if (hasHeaders && parsedData.Count > 0)
            {
                var headers = parsedData[0];

                if (columns.Count == 0)
                {
                    for (int i = 0; i < headers.Count; i++)
                    {
                        columns.Add(new ListViewColumn
                        {
                            columnName = string.IsNullOrWhiteSpace(headers[i]) ? $"Column {i + 1}" : headers[i].Trim(),
                            useFlexibleWidth = true,
                            alignment = TextAnchor.MiddleCenter
                        });
                    }
                }
                else
                {
                    for (int i = 0; i < headers.Count; i++)
                    {
                        string n = string.IsNullOrWhiteSpace(headers[i]) ? $"Column {i + 1}" : headers[i].Trim();
                        if (i < columns.Count) { columns[i].columnName = n; }
                        else
                        {
                            columns.Add(new ListViewColumn
                            {
                                columnName = n,
                                useFlexibleWidth = true,
                                alignment = TextAnchor.MiddleCenter
                            });
                        }
                    }
                }

                startRow = 1;
            }
            else if (columns.Count == 0 && parsedData.Count > 0)
            {
                for (int i = 0; i < parsedData[0].Count; i++)
                {
                    columns.Add(new ListViewColumn
                    {
                        columnName = $"Column {i + 1}",
                        useFlexibleWidth = true,
                        alignment = TextAnchor.MiddleCenter
                    });
                }
            }

            for (int i = startRow; i < parsedData.Count; i++)
            {
                var rowData = parsedData[i];
                var row = new ListViewRow();
               
                for (int j = 0; j < columns.Count; j++)
                    row.SetCell(j, new ListViewCellData(ListViewCellType.None, j < rowData.Count ? rowData[j] : string.Empty));
               
                rows.Add(row);
            }

            Changed();
        }

        /// <summary>
        /// Serializes the current columns and rows as CSV text.
        /// </summary>
        public string ExportToCSV(bool includeHeaders = true)
        {
            var csv = new StringBuilder();

            if (includeHeaders && columns.Count > 0)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    csv.Append(EscapeCSVValue(columns[i].columnName));
                    if (i < columns.Count - 1) { csv.Append(","); }
                }
                csv.AppendLine();
            }

            foreach (var row in rows)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    csv.Append(EscapeCSVValue(i < row.values.Count ? row.values[i] : ""));
                    if (i < columns.Count - 1) csv.Append(",");
                }
                csv.AppendLine();
            }

            return csv.ToString();
        }

        static List<List<string>> ParseCSV(string csvText)
        {
            var result = new List<List<string>>();
            var currentRow = new List<string>();
            var currentCell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csvText.Length; i++)
            {
                char c = csvText[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < csvText.Length && csvText[i + 1] == '"')
                    {
                        currentCell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                }
                else if ((c == '\n' || c == '\r') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                        i++;

                    currentRow.Add(currentCell.ToString());
                    currentCell.Clear();
                    result.Add(currentRow);
                    currentRow = new List<string>();
                }
                else
                {
                    currentCell.Append(c);
                }
            }

            if (currentCell.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(currentCell.ToString());
                result.Add(currentRow);
            }

            return result;
        }

        static string EscapeCSVValue(string val)
        {
            if (string.IsNullOrEmpty(val))
                return "";

            if (val.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
            {
                val = val.Replace("\"", "\"\"");
                return $"\"{val}\"";
            }

            return val;
        }

        /// <summary>
        /// Writes the current ListView data to a UTF-8 CSV file.
        /// </summary>
        public void SaveToCSVFile(string path, bool headers = true)
        {
            try
            {
                File.WriteAllText(path, ExportToCSV(headers), Encoding.UTF8);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        /// <summary>
        /// Loads ListView data from a UTF-8 CSV file when it exists.
        /// </summary>
        public void LoadFromCSVFile(string path, bool headers = true, bool clear = true)
        {
            try
            {
                if (File.Exists(path))
                    ImportFromCSV(File.ReadAllText(path, Encoding.UTF8), headers, clear);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }
    }
}