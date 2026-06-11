using MaterialSkin;
using MaterialSkin.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Terminal_POS
{
    public partial class Main : Form
    {
        enum TerminalState
        {
            Idle,
            Selection,
            EnterAmount,
            WaitCard,
            Processing,
            EnterPin,
            EnterReceiptNumber,
            EnterRefundAmount
        }
        TerminalState state = TerminalState.Idle;
        enum OperationType
        {
            Payment,
            Refund
        }
        OperationType currentOperation = OperationType.Payment;

        //==========ІМПОРТИДЛЯ ПЕРТЯГУВАННЯ ВІКНА========
        // Імпортуємо функції з user32.dll для перетягування вікна
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture(); // Звільняє захоплення миші

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam); // Відправляє повідомлення вікну

        // ===============ОГОЛОШЕННЯ ЗМІНИХ ТА КЛАСІВ====================
        System.Windows.Forms.Label[] labels; // Мітки меню
        int currentReceiptNumber = 0; // Поточний номер чека
        int selectedIndex = 0; // Вибрана позиція у меню

        // Збереження чеків
        private System.Collections.Generic.List<int> receiptNumbers = new System.Collections.Generic.List<int>(); // Номери успішних чеків
        private System.Collections.Generic.Dictionary<int, decimal> receiptMap = new System.Collections.Generic.Dictionary<int, decimal>(); // Суми по чеку
        private System.Windows.Forms.Timer _clockTimer; // Таймер годинника
        private bool forceSuccess = true; // Примусове успіх/відмова для тесту
        private const int V = 20; // Код для перетягування вікна

        // Класи UI та звуків
        Style uiStyle = new Style(); // Стилі інтерфейсу
        Sound sound = new Sound(); // Відтворення звуків

        // ===============КОНСТРУКТОРИ ТА ІНІЦІАЛІЗАЦІЯ====================
        public Main()// Конструктор форми, де встановлюємо стиль та обробники подій
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None; // Прибираємо стандартну рамку вікна
            this.MinimumSize = this.Size;
            this.MaximumSize = this.Size;

            labels = new System.Windows.Forms.Label[] { oplata_label, return_label }; // Ініціалізуємо масив міток для вибору операції
            UpdateSelected();
        }

        //========МЕТОДИ========
        private void Main_Load(object sender, EventArgs e)
        {
            // Застосовуємо стилі до панелей
            uiStyle.PanelRoundedUI(panel1, 15);
            uiStyle.PanelRoundedUI(panel2, 25);
            uiStyle.PanelShape(panel2, 40);

            // Ініціалізуємо таймер для оновлення часу та дати
            _clockTimer = new System.Windows.Forms.Timer();
            _clockTimer.Interval = 1000; // Оновлення кожну секунду
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            UpdateSelected(); // Оновлюємо стиль вибраної мітки

            n0_btn.Click += Number_Click;
            n1_btn.Click += Number_Click;
            n2_btn.Click += Number_Click;
            n3_btn.Click += Number_Click;
            n4_btn.Click += Number_Click;
            n5_btn.Click += Number_Click;
            n6_btn.Click += Number_Click;
            n7_btn.Click += Number_Click;
            n8_btn.Click += Number_Click;
            n9_btn.Click += Number_Click;

            cancelOperation_btn.Click += cancelOperation_btn_Click;
            edit_btn.Click += edit_btn_Click;

            suma_tb.ReadOnly = true; // Забороняємо користувачу вводити текст вручну, щоб він міг вводити тільки через кнопки
            suma_tb.TextAlign = HorizontalAlignment.Center;// Вирівнюємо текст по центру для кращого вигляду
        }
        private void ClockTimer_Tick(object sender, EventArgs e) // Оновлює час та дату на мітках
        {
            // Оновлюємо час та дату на відповідних мітках
            Clock_label.Text = DateTime.Now.ToString("HH:mm:ss");
            Data_label.Text = DateTime.Now.ToString("dd.MM.yyyy");
        }
        public bool CheckInternet() // Перевіряє наявність інтернет-з'єднання шляхом пінгування google.com
        {
            try
            {
                Ping ping = new Ping();
                PingReply reply = ping.Send("google.com", 1000); // Пінгуємо google.com з таймаутом 1 секунда
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
        private void UpdateSelected() // Оновлює стиль вибраної мітки
        {
            if (labels == null || labels.Length == 0) return;
            if (selectedIndex < 0 || selectedIndex >= labels.Length) selectedIndex = 0;

            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].BackColor = (i == selectedIndex) ? Color.Gray : Color.Silver;
                labels[i].ForeColor = Color.Black;
            }
        }
        private async void HandleEnter() // Обробляє натискання Enter на вибраній мітці
        {
            // Закриваємо панель вибору і переходимо до введення суми
            selection_panel.SendToBack();

            if (selectedIndex == 0)
            {
                currentOperation = OperationType.Payment;
                log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Обрана операція: Оплата");
                oplata_panel.BringToFront();
                suma_tb.Clear();
                state = TerminalState.EnterAmount;
            }
            else if (selectedIndex == 1)
            {
                currentOperation = OperationType.Refund;
                log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Обрана операція: Повернення");
                if (return_panel1 != null) { return_panel1.Visible = true; return_panel1.BringToFront(); }
                if (return_panel2 != null) return_panel2.Visible = false;
                reciept_tb.Clear();
                state = TerminalState.EnterReceiptNumber;
            }
        }
        private void Number_Click(object sender, EventArgs e) // Обробляє натискання кнопок з цифрами для введення суми або PIN-коду
        {

            var btn = sender as Guna.UI2.WinForms.Guna2Button;

            if (state == TerminalState.EnterAmount)
            {
                sound.PlaySound("click.wav");
                suma_tb.Text += btn.Text;
            }
            else if (state == TerminalState.EnterPin)
            {
                sound.PlaySound("click.wav");
                if (pin_tb.Text.Length < 4)
                    pin_tb.Text += btn.Text;
            }
            else if (currentOperation == OperationType.Refund)
            {
                sound.PlaySound("click.wav");
                if (state == TerminalState.EnterReceiptNumber)
                {
                    reciept_tb.Text += btn.Text;
                }
                else if (state == TerminalState.EnterRefundAmount)
                {
                    suma_returnt_tb.Text += btn.Text;
                }
            }
        }
        private async Task HadleEnterUniversal() // Універсальний обробник Enter
        {
            switch (state)
            {
                case TerminalState.Idle:
                    // Показати меню вибору операції
                    selection_panel.Visible = true;
                    selection_panel.BringToFront();
                    selection_panel.Refresh();
                    state = TerminalState.Selection;
                    UpdateSelected();
                    break;

                case TerminalState.Selection:
                    // Підтвердження вибору
                    UpdateSelected();
                    HandleEnter();
                    break;

                case TerminalState.EnterAmount:
                    // Перевірка введеної суми
                    if (string.IsNullOrWhiteSpace(suma_tb.Text))
                    {
                        check_label.Visible = true;
                        await Task.Delay(500);
                        check_label.Visible = false;
                        return;
                    }

                    // Якщо повернення — запит номера чека
                    if (currentOperation == OperationType.Refund)
                    {
                        if (return_panel1 != null) { return_panel1.BringToFront(); }
                        reciept_tb.Clear();
                        state = TerminalState.EnterReceiptNumber;
                        return;
                    }

                    // Інакше — просимо прикласти карту для платежу
                    main_panel.BringToFront();
                    main_label.Text = "Прикладіть карту";
                    main_label.Location = new Point(75, 69);
                    state = TerminalState.WaitCard;
                    break;

                case TerminalState.EnterReceiptNumber:
                    // Валідація номера чека для повернення
                    if (!int.TryParse(reciept_tb.Text, out int receiptNum))
                    {
                        label14.Visible = true;
                        label14.Location = new Point(83, 81);
                        await Task.Delay(700);
                        label14.Visible = false;
                        return;
                    }

                    if (!receiptNumbers.Contains(receiptNum))
                    {
                        label14.Visible = true;
                        label14.Location = new Point(101, 81);
                        label14.Text = "Чек не знайдено";
                        await Task.Delay(700);
                        label14.Visible = false;
                        return;
                    }

                    currentReceiptNumber = receiptNum;
                    if (return_panel1 != null) return_panel1.Visible = false;
                    if (return_panel2 != null) { return_panel2.Visible = true; return_panel2.BringToFront(); }
                    suma_returnt_tb.Clear();
                    state = TerminalState.EnterRefundAmount;
                    break;

                case TerminalState.EnterRefundAmount:
                    // Підтвердження суми повернення
                    return_panel2.BringToFront();

                    if (!decimal.TryParse(suma_returnt_tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal enteredAmount) &&
                        !decimal.TryParse(suma_returnt_tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out enteredAmount))
                    {
                        label19.Visible = true;
                        await Task.Delay(700);
                        label19.Visible = false;
                        return;
                    }

                    awai_panel.BringToFront();
                    awai_lebel.Text = "З'єднання з банком...";
                    state = TerminalState.Processing;

                    await Task.Delay(2000);

                    bool refundSuccess = await FakeBankRequest();

                    // Підготовка чека і показ результату
                    suma_tb.Text = enteredAmount.ToString(CultureInfo.CurrentCulture);
                    PrintReceipt(refundSuccess);
                    ShowResult(refundSuccess);

                    if (refundSuccess)
                    {
                        receiptNumbers.Remove(currentReceiptNumber);
                        receiptMap.Remove(currentReceiptNumber);
                    }

                    await Task.Delay(1000);
                    ResetToMain();
                    break;

                case TerminalState.EnterPin:
                    // Перевірка PIN
                    if (pin_tb.Text.Length < 4)
                    {
                        check_pin_label.Visible = true;
                        await Task.Delay(1000);
                        check_pin_label.Visible = false;
                        return;
                    }

                    // Перевірка і парсинг суми
                    if (!decimal.TryParse(suma_tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal paymentAmount) &&
                        !decimal.TryParse(suma_tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out paymentAmount))
                    {
                        PrintReceipt(false);
                        ShowResult(false);
                        await Task.Delay(1000);
                        ResetToMain();
                        return;
                    }

                    // Завжди показуємо екран з'єднання з банком
                    awai_panel.BringToFront();
                    awai_lebel.Text = "З'єднання з банком...";
                    state = TerminalState.Processing;

                    await Task.Delay(2000);

                    bool success = false;
                    if (Balance.HasSufficientFunds(balance_tb, paymentAmount))
                    {
                        success = await FakeBankRequest();
                    }

                    PrintReceipt(success);
                    ShowResult(success);

                    await Task.Delay(1000);
                    ResetToMain();
                    break;
            }
        }
        class Balance // Внутрішній клас для управління балансом
        {
            public static decimal GetBalance(TextBox tb) // Отримує поточний баланс з текстового поля
            {
                if (tb == null) return 0m;
                var text = tb.Text ?? string.Empty;
                if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var v))
                    return v;
                if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
                    return v;
                return 0m;
            }
            public static void SetBalance(TextBox tb, decimal value) // Встановлює новий баланс у текстове поле
            {
                if (tb == null) return;
                tb.Text = value.ToString(CultureInfo.CurrentCulture);
            }
            public static bool TrySubtract(TextBox tb, decimal amount) // Спробує відняти суму від балансу, повертає false, якщо недостатньо коштів
            {
                if (tb == null) return false;
                var cur = GetBalance(tb);
                if (cur < amount) return false;
                SetBalance(tb, cur - amount);
                return true;
            }
            public static void Add(TextBox tb, decimal amount) // Додає суму до балансу
            {
                if (tb == null) return;
                var cur = GetBalance(tb);
                SetBalance(tb, cur + amount);
            }
            public static bool HasSufficientFunds(TextBox tb, decimal amount) // Перевіряє чи вистачає коштів
            {
                if (tb == null) return false;
                var cur = GetBalance(tb);
                return cur >= amount;
            }
        }




        private void ResetToMain() // Скидає інтерфейс до початкового стану для нової операції
        {
            main_panel.BringToFront();
            suma_tb.Clear();
            pin_tb.Clear();

            try
            {
                if (return_panel1 != null) return_panel1.Visible = false;
                if (return_panel2 != null) return_panel2.Visible = false;
                if (reciept_tb != null) reciept_tb.Clear();
                if (suma_returnt_tb != null) suma_returnt_tb.Clear();
            }
            catch { }

            state = TerminalState.Idle;

            main_label.Text = "Виберіть дію!";
            main_label.Location = new Point(89, 69);
        }
        private void PrintReceipt(bool success) // Імітує друк чека
        {
            int receiptNumber = new Random().Next(100000, 999999);
            // Зберігаємо номер чека у масиві тільки якщо це оплата і операція успішна
            if (success && currentOperation == OperationType.Payment)
            {
                receiptNumbers.Add(receiptNumber);

                // Якщо це оплата і успішна — зберігаємо суму по номеру чека
                if (decimal.TryParse(suma_tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal amt) ||
                    decimal.TryParse(suma_tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out amt))
                {
                    receiptMap[receiptNumber] = amt;
                }
            }

            receipt_listBox.Items.Add("----- ЧЕК -----");
            receipt_listBox.Items.Add($"Номер чека: {receiptNumber}");
            receipt_listBox.Items.Add($"Дата: {DateTime.Now:dd.MM.yyyy} Час: {DateTime.Now:HH:mm:ss}");
            receipt_listBox.Items.Add($"Сума: {suma_tb.Text}");
            receipt_listBox.Items.Add($"Статус: {(success ? "УСПІХ" : "ВІДМОВА")}");
            receipt_listBox.Items.Add("----------------");

            try
            {
                if (success)
                {
                    if (currentOperation == OperationType.Payment)
                    {
                        if (decimal.TryParse(suma_tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal amt) ||
                            decimal.TryParse(suma_tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out amt))
                        {
                            Balance.TrySubtract(balance_tb, amt);
                        }
                    }
                    else if (currentOperation == OperationType.Refund)
                    {
                        if (decimal.TryParse(suma_tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal ramt) ||
                            decimal.TryParse(suma_tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out ramt))
                        {
                            Balance.Add(balance_tb, ramt);
                        }
                    }
                }
            }
            catch { }
        }
        private async void ShowResult(bool success) // Показує результат операції та імітує друк чека ??
        {
            if (success)
            {
                log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Оплата пройшла успішно");
            }
            else
            {
                log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Відмова");
            }
        }
        private async Task<bool> FakeBankRequest() // Імітує запит до банку з випадковим результатом успіху або відмови ??
        {
            // Імітуємо невелике мережеве затримання
            await Task.Delay(1000);

            // Імітуємо випадковий результат (наприклад, 75% успіху)
            var rnd = new Random();
            return rnd.Next(100) < 75;
        }

        //=======ЕЛЕМЕНТИ КЕРУВАННЯ=======
        private async void materialSwitch1_CheckedChanged(object sender, EventArgs e) // Обробляє зміну стану перемикача для включення/вимкнення терміналу
        {
            var ms = sender as MaterialSkin.Controls.MaterialSwitch ?? OnOff_switch;
            if (!ms.Checked)
            {
                log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Термінал вимкнено");
                main_label.Visible = true;
                main_label.Text = "До побачення";
                main_label.Location = new Point(90, 69);

                work_pb.Value = 0;
                done_tb.Text = "Не готовий";
                done_tb.BackColor = Color.Crimson;
                done_label.Text = "Офлайн";
                done_label.ForeColor = Color.Crimson;

                await Task.Delay(1500);
                Clock_label.Visible = false;
                Data_label.Visible = false;
                main_label.Visible = false;
                decorationBar.Visible = false;

                return;
            }
            // Якщо перемикач увімкнено, починаємо процес завантаження терміналу
            main_label.Visible = ms.Checked;
            OnOff_switch.Text = ms.Checked ? "Вкл" : "Викл";
            main_label.Text = ms.Checked ? "Вітаю" : "До побачення";
            log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Термінал увімкнено");


            work_pb.Value = 0;
            for (int i = 1; i <= 100; i++)
            {
                await Task.Delay(50);
                work_pb.Value = i;
                if (i == 0)
                {
                    main_label.Text = "Вітаю";
                    main_label.Location = new Point(125, 69);
                }
                else if (i == 20)
                {
                    await Task.Delay(1500);
                    main_label.Text = "Завантажуємо термінал";
                    main_label.Location = new Point(37, 69);
                    log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Завантаження терміналу...");
                    await Task.Delay(1000);
                    log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Завантаження драйверів...");
                    await Task.Delay(1000);
                    log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Ініціалізація обладнання...");

                }
                else if (i == 50)
                {

                    main_label.Text = "Підключення до мережі";
                    await Task.Delay(1000);
                    log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Перевірка інтернет-з'єднання...");
                    bool hasInternet = await Task.Run(() => CheckInternet());

                    if (hasInternet)
                    {
                        main_label.Text = "Мережа підключена";
                        main_label.Location = new Point(52, 69);
                        done_label.Text = "Онлайн";
                        done_label.ForeColor = Color.DarkSeaGreen;
                        log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Інтернет-з'єднання встановлено");
                    }
                    else
                    {
                        main_label.Text = "Немає інтернету";
                        main_label.Location = new Point(68, 69);
                        done_label.Text = "Офлайн";
                        done_label.ForeColor = Color.Red;
                        log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Немає інтернет-з'єднання");
                        await Task.Delay(1000);
                        log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Повторне підключення");
                        while (!hasInternet)
                        {
                            await Task.Delay(1000);

                            hasInternet = await Task.Run(() => CheckInternet());

                            if (hasInternet)
                            {
                                log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Інтернет-з'єднання встановлено");
                                break;
                            }
                            else
                            {
                                log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Немає інтернет-з'єднання");
                            }
                        }
                    }
                }
                else if (i == 70)
                {
                    main_label.Text = "Готовий до роботи";
                    main_label.Location = new Point(67, 69);
                    done_tb.BackColor = Color.DarkSeaGreen;
                    done_tb.Text = "Готовий до роботи!";
                    done_label.Text = "Онлайн";
                    done_label.ForeColor = Color.DarkSeaGreen;
                    log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Термінал готовий до роботи");

                }
                else if (i == 100)
                {
                    main_label.Text = "Виберіть дію";
                    main_label.Location = new Point(89, 69);
                    work_pb.Value = 0;
                    Clock_label.Visible = true;
                    Data_label.Visible = true;
                    decorationBar.Visible = true;
                }
            }
        }

        private void card_btn_Click(object sender, EventArgs e)
        {
            if (state != TerminalState.WaitCard)
                return;

            log_listBox.Items.Add($"{DateTime.Now:HH:mm:ss} - Карта прикладена");
            pin_panel.BringToFront();
            pin_tb.Clear();
            state = TerminalState.EnterPin;
        }
        private void down_btn_Click(object sender, EventArgs e) // Обробляє натискання кнопки для переміщення вниз по списку міток
        {
            sound.PlaySound("click.wav");
            if (state == TerminalState.Idle || state == TerminalState.Selection)
            {
                if (labels == null || labels.Length == 0) return;
                selectedIndex++;
                if (selectedIndex >= labels.Length) selectedIndex = 0;
                UpdateSelected();
            }
        }
        private void up_btn_Click(object sender, EventArgs e) // Обробляє натискання кнопки для переміщення вгору по списку міток
        {
            sound.PlaySound("click.wav");
            if (state == TerminalState.Idle || state == TerminalState.Selection)
            {
                selectedIndex--;
                if (selectedIndex < 0)
                    selectedIndex = labels.Length - 1;
                UpdateSelected();
            }
        }
        private async void enter_btn_Click(object sender, EventArgs e) // Обробляє натискання кнопки для вибору поточної мітки
        {
            sound.PlaySound("click.wav");
            await HadleEnterUniversal();
        }
        private void clearLog_btn_Click(object sender, EventArgs e)
        {
            log_listBox.Items.Clear();
        }
        private void F1_menu_btn_Click(object sender, EventArgs e)
        {
            ResetToMain();
        }
        private void cancelOperation_btn_Click(object sender, EventArgs e) // Обробляє натискання кнопки для скасування операції
        {
            sound.PlaySound("click.wav");
            ResetToMain();
            if (suma_tb.Text.Length > 0)
            {
                suma_tb.Text = suma_tb.Text.Substring(0, suma_tb.Text.Length - 1);
            }
            selection_panel.SendToBack();
            oplata_panel.SendToBack();
            main_label.Visible = true;
            main_label.Text = "Виберіть дію!";
            main_label.Location = new Point(89, 69);
            suma_tb.Clear();
        }
        private void edit_btn_Click(object sender, EventArgs e) // Обробляє натискання кнопки для редагування суми
        {
            sound.PlaySound("click.wav");
            suma_tb.Clear();
            reciept_tb.Clear();
            suma_returnt_tb.Clear();
        }

        private void EXIT_button_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
