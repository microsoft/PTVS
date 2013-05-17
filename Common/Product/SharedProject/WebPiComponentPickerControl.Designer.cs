namespace Microsoft.VisualStudioTools.Project {
    partial class WebPiComponentPickerControl {
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
            this.backgroundWorker5 = new System.ComponentModel.BackgroundWorker();
            this._addNewFeed = new System.Windows.Forms.Button();
            this._productsList = new System.Windows.Forms.ListView();
            this._name = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._released = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._feed = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._newFeedUrl = new System.Windows.Forms.TextBox();
            this._addNewFeedLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // _addNewFeed
            // 
            this._addNewFeed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._addNewFeed.Location = new System.Drawing.Point(388, 406);
            this._addNewFeed.MaximumSize = new System.Drawing.Size(110, 23);
            this._addNewFeed.Name = "_addNewFeed";
            this._addNewFeed.Size = new System.Drawing.Size(110, 23);
            this._addNewFeed.TabIndex = 4;
            this._addNewFeed.Text = "Add New Feed...";
            this._addNewFeed.UseVisualStyleBackColor = true;
            this._addNewFeed.Click += new System.EventHandler(this.AddNewFeedClick);
            // 
            // _productsList
            // 
            this._productsList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._productsList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._name,
            this._released,
            this._feed});
            this._productsList.Location = new System.Drawing.Point(7, 4);
            this._productsList.Name = "_productsList";
            this._productsList.Size = new System.Drawing.Size(491, 397);
            this._productsList.TabIndex = 0;
            this._productsList.UseCompatibleStateImageBehavior = false;
            this._productsList.View = System.Windows.Forms.View.Details;
            // 
            // _name
            // 
            this._name.Text = "Name";
            this._name.Width = 154;
            // 
            // _released
            // 
            this._released.Text = "Released";
            this._released.Width = 110;
            // 
            // _feed
            // 
            this._feed.Text = "Feed";
            this._feed.Width = 283;
            // 
            // _newFeedUrl
            // 
            this._newFeedUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._newFeedUrl.Location = new System.Drawing.Point(69, 407);
            this._newFeedUrl.Name = "_newFeedUrl";
            this._newFeedUrl.Size = new System.Drawing.Size(313, 20);
            this._newFeedUrl.TabIndex = 5;
            this._newFeedUrl.TextChanged += new System.EventHandler(this.NewFeedUrlTextChanged);
            // 
            // _addNewFeedLabel
            // 
            this._addNewFeedLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._addNewFeedLabel.AutoSize = true;
            this._addNewFeedLabel.Location = new System.Drawing.Point(4, 411);
            this._addNewFeedLabel.Name = "_addNewFeedLabel";
            this._addNewFeedLabel.Size = new System.Drawing.Size(59, 13);
            this._addNewFeedLabel.TabIndex = 6;
            this._addNewFeedLabel.Text = "New Feed:";
            // 
            // WebPiComponentPickerControl
            // 
            this.Controls.Add(this._productsList);
            this.Controls.Add(this._addNewFeedLabel);
            this.Controls.Add(this._newFeedUrl);
            this.Controls.Add(this._addNewFeed);
            this.Name = "WebPiComponentPickerControl";
            this.Size = new System.Drawing.Size(501, 433);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.ComponentModel.BackgroundWorker backgroundWorker5;
        private System.Windows.Forms.Button _addNewFeed;
        private System.Windows.Forms.ListView _productsList;
        private System.Windows.Forms.ColumnHeader _name;
        private System.Windows.Forms.ColumnHeader _released;
        private System.Windows.Forms.ColumnHeader _feed;
        private System.Windows.Forms.TextBox _newFeedUrl;
        private System.Windows.Forms.Label _addNewFeedLabel;
    }
}
