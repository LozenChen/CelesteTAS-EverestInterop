using System.Linq;
using CelesteStudio.Editing;
using Eto.Forms;

namespace CelesteStudio.Dialog;

public class GoToDialog : Dialog<int> {
    private GoToDialog(Document document) {
        var lineSelector = new NumericStepper {
            MinValue = 1,
            MaxValue = document.Lines.Count,
            Increment = 1,
            Value = document.Caret.Row + 1,
            Width = 150,
        };
        
        // All labels need to start with a # and immediately follow with the text
        var labels = document.Lines
            .Select((line, row) => (line, row))
            .Where(pair => pair.line.Length >= 2 && pair.line[0] == '#' && char.IsLetter(pair.line[1]))
            .Select(pair => pair with { line = pair.line[1..] }) // Remove the #
            .ToArray();
        
        var dropdown = new DropDown { Width = 150 };
        
        if (labels.Length == 0) {
            dropdown.Enabled = false;    
        } else {
            foreach (var (label, row) in labels) {
                dropdown.Items.Add(new ListItem { Text = label, Key = row.ToString() });
            }
            
            // Find current label
            dropdown.SelectedKey = labels.Reverse().FirstOrDefault(pair => pair.row <= document.Caret.Row, labels[0]).row.ToString();
            dropdown.SelectedKeyChanged += (_, _) => lineSelector.Value = int.Parse(dropdown.SelectedKey);
        }
        
        Title = "Go To";
        Content = new StackLayout {
            Padding = 10,
            Spacing = 10,
            Items = {
                new StackLayout {
                    Spacing = 10,
                    Orientation = Orientation.Horizontal,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Items = { new Label { Text = "Line" }, lineSelector }
                },
                new StackLayout {
                    Spacing = 10,
                    Orientation = Orientation.Horizontal,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Items = { new Label { Text = "Label" }, dropdown }
                }
            }
        };
        
        DefaultButton = new Button((_, _) => Close((int)lineSelector.Value - 1)) { Text = "&Go" };
        AbortButton = new Button((_, _) => Close(document.Caret.Row)) { Text = "&Cancel" };
        
        PositiveButtons.Add(DefaultButton);
        NegativeButtons.Add(AbortButton);
    }
    
    public static int Show(Document document) => new GoToDialog(document).ShowModal();
}