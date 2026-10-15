// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Small tool window for the Kestrel copy/paste tool. Opened with F12 or the "copypaste" console command.

using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Kestrel.Mapping;

public sealed class KestrelCopyPasteWindow : DefaultWindow
{
    public readonly Button CopyButton;
    public readonly Button PasteButton;
    public readonly Button RotateButton;
    public readonly Button FlipHButton;
    public readonly Button FlipVButton;
    public readonly Button UndoButton;
    public readonly CheckBox LockCheckBox;
    public readonly Label TransformLabel;
    public readonly Label HintLabel;
    public readonly Label StatusLabel;

    public KestrelCopyPasteWindow()
    {
        Title = Loc.GetString("kestrel-copypaste-window-title");
        MinSize = new Vector2(320, 0);

        CopyButton = new Button
        {
            Text = Loc.GetString("kestrel-copypaste-window-copy"),
            ToggleMode = true,
            HorizontalExpand = true,
        };

        PasteButton = new Button
        {
            Text = Loc.GetString("kestrel-copypaste-window-paste"),
            ToggleMode = true,
            HorizontalExpand = true,
        };

        RotateButton = new Button
        {
            Text = Loc.GetString("kestrel-copypaste-window-rotate"),
            HorizontalExpand = true,
        };

        FlipHButton = new Button
        {
            Text = Loc.GetString("kestrel-copypaste-window-flip-h"),
            HorizontalExpand = true,
        };

        FlipVButton = new Button
        {
            Text = Loc.GetString("kestrel-copypaste-window-flip-v"),
            HorizontalExpand = true,
        };

        UndoButton = new Button
        {
            Text = Loc.GetString("kestrel-copypaste-window-undo", ("count", 0)),
            HorizontalExpand = true,
        };

        LockCheckBox = new CheckBox
        {
            Text = Loc.GetString("kestrel-copypaste-window-lock"),
        };

        TransformLabel = new Label();
        HintLabel = new Label { FontColorOverride = Color.LightGray };
        StatusLabel = new Label();

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children = { CopyButton, PasteButton },
                },
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children = { RotateButton, FlipHButton, FlipVButton },
                },
                TransformLabel,
                LockCheckBox,
                UndoButton,
                HintLabel,
                StatusLabel,
            },
        });
    }
}
