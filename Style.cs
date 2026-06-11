using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Terminal_POS
{
    public class Style
    {
        public void PanelRoundedUI(Control control, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, radius, radius, 180, 90);
            path.AddArc(control.Width - radius, 0, radius, radius, 270, 90);
            path.AddArc(control.Width - radius, control.Height - radius, radius, radius, 0, 90);
            path.AddArc(0, control.Height - radius, radius, radius, 90, 90);
            path.CloseAllFigures();
            control.Region = new Region(path);
        }
        public void PanelShape(Panel panel2, int curveHeight = 40)
        {
            GraphicsPath path = new GraphicsPath();

            int w = panel2.Width;
            int h = panel2.Height;
            path.StartFigure();
            path.AddLine(0, 0, w, 0);
            path.AddLine(w, 0, w, h - curveHeight);

            // Вигнутий низ
            path.AddBezier(
                w, h - curveHeight,
                w - 100, h,
                100, h,
                0, h - curveHeight
            );

            // Ліва сторона
            path.AddLine(0, h - curveHeight, 0, 0);
            path.CloseFigure();

            panel2.Region = new Region(path);
        }
    }
}
