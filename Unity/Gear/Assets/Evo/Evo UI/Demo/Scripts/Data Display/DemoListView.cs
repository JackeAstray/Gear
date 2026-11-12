using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// List View tab: fills a leaderboard with the chosen number of rows and sets the style options.
    /// The list's own scroll settings stay as they are: Fixed Scale Mode and Virtualize Rows keep only the rows in view.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoListView : MonoBehaviour
    {
        [SerializeField] private ListView list;
        [SerializeField] private int[] rowCounts = { 5, 10, 100 };

        static readonly string[] Syllables = { "ka", "ri", "no", "vex", "lo", "mar", "zen", "ta", "qu", "il", "dra", "sol", "nyx", "ro", "ae", "fen", "ix", "or", "kai", "mo" };

        readonly List<ListViewRow> generated = new();

        public void SetRows(int index)
        {
            int count = rowCounts[index];
            Generate(count);

            List<ListViewRow> rows = list.rows;
            rows.Clear();

            for (int i = 0; i < count; i++)
                rows.Add(generated[i]);

            list.ScrollToRow(0, 0, true);
            list.Refresh();
        }

        public void SetShowBorder(bool value) => Apply(ref list.style.showBorder, value);
        public void SetRowHeight(float value) => Apply(ref list.style.rowHeight, value);
        public void SetRowSpacing(float value) => Apply(ref list.style.rowSpacing, value);
        public void SetColumnSpacing(float value) => Apply(ref list.style.columnSpacing, value);

        void Apply<T>(ref T field, T value)
        {
            field = value;
            list.Refresh();
        }

        // Same rows for the same rank every time, built once and shared by every row count
        void Generate(int count)
        {
            for (int i = generated.Count; i < count; i++)
            {
                var random = new System.Random(i * 7919 + 17);
                string name = "";
                int syllables = 2 + random.Next(2);

                for (int s = 0; s < syllables; s++)
                    name += Syllables[random.Next(Syllables.Length)];
                name = char.ToUpperInvariant(name[0]) + name.Substring(1) + (random.Next(3) == 0 ? random.Next(99).ToString() : "");

                int rating = Mathf.RoundToInt(3600 - 1500 * Mathf.Pow(i / 10000f, 0.55f));
                generated.Add(new ListViewRow((i + 1).ToString("N0"), name, (40 + random.Next(900)).ToString("N0"),
                    (38 + random.NextDouble() * 30).ToString("0") + "%", rating.ToString("N0")));
            }
        }
    }
}