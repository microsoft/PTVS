namespace Microsoft.PythonTools.Options {
    partial class PythonFormattingOptionsControl {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            this._optionsTree = new Microsoft.PythonTools.Options.OptionsTreeView();
            this._editorHost = new System.Windows.Forms.Integration.ElementHost();
            this.SuspendLayout();
            // 
            // _optionsTree
            // 
            this._optionsTree.Dock = System.Windows.Forms.DockStyle.Top;
            this._optionsTree.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
            this._optionsTree.HotTracking = true;
            this._optionsTree.Location = new System.Drawing.Point(0, 0);
            this._optionsTree.Margin = new System.Windows.Forms.Padding(0);
            this._optionsTree.Name = "_optionsTree";
            this._optionsTree.ShowLines = false;
            this._optionsTree.Size = new System.Drawing.Size(383, 174);
            this._optionsTree.TabIndex = 0;
            // 
            // _editorHost
            // 
            this._editorHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._editorHost.Location = new System.Drawing.Point(0, 174);
            this._editorHost.Name = "_editorHost";
            this._editorHost.Size = new System.Drawing.Size(383, 106);
            this._editorHost.TabIndex = 2;
            this._editorHost.Text = "elementHost1";
            this._editorHost.Child = null;
            // 
            // PythonFormattingOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._editorHost);
            this.Controls.Add(this._optionsTree);
            this.Name = "PythonFormattingOptionsControl";
            this.Size = new System.Drawing.Size(383, 280);
            this.ResumeLayout(false);

        }

        #endregion

        private OptionsTreeView _optionsTree;
        private System.Windows.Forms.Integration.ElementHost _editorHost;

    }
}
