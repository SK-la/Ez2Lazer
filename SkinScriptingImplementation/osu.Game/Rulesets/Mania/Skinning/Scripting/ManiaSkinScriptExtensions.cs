using MoonSharp.Interpreter;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Skinning.Scripting
{
    /// <summary>
    /// Provides mania-specific extensions for skin scripts.
    /// </summary>
    [MoonSharpUserData]
    public class ManiaSkinScriptExtensions
    {
        private readonly ManiaAction[] columnBindings;
        private readonly StageDefinition stageDefinition;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManiaSkinScriptExtensions"/> class.
        /// </summary>
        /// <param name="stage">The stage this extension is for.</param>
        public ManiaSkinScriptExtensions(Stage stage)
        {
            stageDefinition = stage.Definition;

            // Store column bindings
            columnBindings = new ManiaAction[stageDefinition.Columns];
            for (int i = 0; i < stageDefinition.Columns; i++)
            {
                columnBindings[i] = stageDefinition.GetActionForColumn(i);
            }
        }

        /// <summary>
        /// Gets the number of columns in the stage.
        /// </summary>
        /// <returns>The number of columns.</returns>
        [MoonSharpVisible(true)]
        public int GetColumnCount()
        {
            return stageDefinition.Columns;
        }

        /// <summary>
        /// Gets the column index for a specific note.
        /// </summary>
        /// <param name="note">The note.</param>
        /// <returns>The column index.</returns>
        [MoonSharpVisible(true)]
        public int GetNoteColumn(Note note)
        {
            return note.Column;
        }

        /// <summary>
        /// Gets the binding (action) for a specific column.
        /// </summary>
        /// <param name="column">The column index.</param>
        /// <returns>The binding action as a string.</returns>
        [MoonSharpVisible(true)]
        public string GetColumnBinding(int column)
        {
            if (column < 0 || column >= columnBindings.Length)
                return "Invalid";

            return columnBindings[column].ToString();
        }

        /// <summary>
        /// Gets the width of a specific column.
        /// </summary>
        /// <param name="column">The column index.</param>
        /// <returns>The column width.</returns>
        [MoonSharpVisible(true)]
        public float GetColumnWidth(int column)
        {
            if (column < 0 || column >= stageDefinition.Columns)
                return 0;

            return stageDefinition.ColumnWidths[column];
        }
    }
}
