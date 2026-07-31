namespace CDS.ScriptChat.WinForms;

partial class ChatTurnView
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null!;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Component Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        _layout = new System.Windows.Forms.TableLayoutPanel();
        _roleLabel = new System.Windows.Forms.Label();
        _messageLabel = new System.Windows.Forms.Label();
        _codeTextBox = new System.Windows.Forms.TextBox();
        _layout.SuspendLayout();
        SuspendLayout();
        //
        // _layout
        //
        _layout.AutoSize = true;
        _layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        _layout.Controls.Add(_roleLabel, 0, 0);
        _layout.Controls.Add(_messageLabel, 0, 1);
        _layout.Controls.Add(_codeTextBox, 0, 2);
        _layout.Dock = System.Windows.Forms.DockStyle.Top;
        _layout.Location = new System.Drawing.Point(0, 0);
        _layout.Margin = new System.Windows.Forms.Padding(0);
        _layout.Name = "_layout";
        _layout.Padding = new System.Windows.Forms.Padding(8);
        _layout.RowCount = 3;
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        _layout.Size = new System.Drawing.Size(400, 68);
        _layout.TabIndex = 0;
        //
        // _roleLabel
        //
        _roleLabel.AutoSize = true;
        _roleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold);
        _roleLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _roleLabel.Location = new System.Drawing.Point(8, 8);
        _roleLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
        _roleLabel.Name = "_roleLabel";
        _roleLabel.Size = new System.Drawing.Size(30, 15);
        _roleLabel.TabIndex = 0;
        _roleLabel.Text = "You";
        //
        // _messageLabel
        //
        _messageLabel.AutoSize = true;
        _messageLabel.Location = new System.Drawing.Point(8, 27);
        _messageLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
        _messageLabel.Name = "_messageLabel";
        _messageLabel.Size = new System.Drawing.Size(0, 15);
        _messageLabel.TabIndex = 1;
        //
        // _codeTextBox
        //
        _codeTextBox.BackColor = System.Drawing.SystemColors.Window;
        _codeTextBox.Font = new System.Drawing.Font("Cascadia Mono", 9F);
        _codeTextBox.Location = new System.Drawing.Point(8, 46);
        _codeTextBox.Margin = new System.Windows.Forms.Padding(0);
        _codeTextBox.Multiline = true;
        _codeTextBox.Name = "_codeTextBox";
        _codeTextBox.ReadOnly = true;
        _codeTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        _codeTextBox.Size = new System.Drawing.Size(384, 160);
        _codeTextBox.TabIndex = 2;
        _codeTextBox.Visible = false;
        _codeTextBox.WordWrap = false;
        //
        // ChatTurnView
        //
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        Controls.Add(_layout);
        Margin = new System.Windows.Forms.Padding(0);
        Name = "ChatTurnView";
        Size = new System.Drawing.Size(400, 68);
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.Label _roleLabel;
    private System.Windows.Forms.Label _messageLabel;
    private System.Windows.Forms.TextBox _codeTextBox;
}
