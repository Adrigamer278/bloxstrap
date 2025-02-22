using System.Windows.Forms;

using Bloxstrap.UI.Elements.Bootstrapper.Base;

namespace Bloxstrap.UI.Elements.Bootstrapper
{
    // windows: https://youtu.be/VpduiruysuM?t=18
    // mac: https://youtu.be/ncHhbcVDRgQ?t=63

    public partial class LegacyDialog2008 : WinFormsDialogBase
    {
        protected override string _message
        {
            get => labelMessage.Text;
            set => labelMessage.Text = value;
        }

        protected override ProgressBarStyle _progressStyle
        {
            get => ProgressBar.Style;
            set => ProgressBar.Style = value;
        }

        protected override int _progressMaximum
        {
            get => ProgressBar.Maximum;
            set => ProgressBar.Maximum = value;
        }

        protected override int _progressValue
        {
            get => ProgressBar.Value;
            set => ProgressBar.Value = value;
        }

        protected override bool _cancelEnabled
        {
            get => buttonCancel.Enabled;
            set => buttonCancel.Enabled = value;
        }

        public LegacyDialog2008()
        {
            InitializeComponent();

            buttonCancel.Text = Strings.Common_Cancel;

            ScaleWindow();
            SetupDialog();

            ProgressBar.RightToLeft = RightToLeft;
            ProgressBar.RightToLeftLayout = RightToLeftLayout;
        }

        private void LegacyDialog2008_Load(object sender, EventArgs e)
        {
            Activate();
        }
    }
}
