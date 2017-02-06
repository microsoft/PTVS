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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonFormattingOptionsControl));
            this._optionsTree = new Microsoft.PythonTools.Options.OptionsTreeView();
            this._editorHost = new System.Windows.Forms.Integration.ElementHost();
            this.SuspendLayout();
            // 
            // _optionsTree
            // 
            resources.ApplyResources(this._optionsTree, "_optionsTree");
            this._optionsTree.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawText;
            this._optionsTree.HotTracking = true;
            this._optionsTree.Name = "_optionsTree";
            this._optionsTree.ShowLines = false;
            // 
            // _editorHost
            // 
            resources.ApplyResources(this._editorHost, "_editorHost");
            this._editorHost.Name = "_editorHost";
            this._editorHost.Child = null;
            // 
            // PythonFormattingOptionsControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._editorHost);
            this.Controls.Add(this._optionsTree);
            this.Name = "PythonFormattingOptionsControl";
            this.ResumeLayout(false);

        }

        #endregion

        private OptionsTreeView _optionsTree;
        private System.Windows.Forms.Integration.ElementHost _editorHost;

    }
}
