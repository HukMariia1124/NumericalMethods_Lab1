using NCalc;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Drawing.Imaging.Effects;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Lab1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        /// Використовуємо NCalc для обчислення значення функції
        double SolveFunction(string expr, double x)
        {
            NCalc.Expression e = new NCalc.Expression(expr);
            e.EvaluateFunction += delegate (string name, NCalc.FunctionArgs args)
            {
                if (name == "Abs")
                {
                    args.Result = Math.Abs(Convert.ToDouble(args.Parameters[0].Evaluate()));
                }
            };
            e.Parameters["x"] = x;
            e.Parameters["PI"] = Math.PI;
            return Convert.ToDouble(e.Evaluate(), CultureInfo.InvariantCulture);
        }
        /// Основна логіка обробки кліку на кнопку "Розв'язати"
        private void Solve_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (equation.Text.Length == 0)
                {
                    res.Text = "Введіть рівняння";
                    return;
                }
                if (start.Text.Length == 0)
                {
                    res.Text = "Введіть початок проміжку";
                    return;
                }
                if (end.Text.Length == 0)
                {
                    res.Text = "Введіть кінець проміжку";
                    return;
                }
                if (eps.Text.Length == 0)
                {
                    res.Text = "Введіть епсилон";
                    return;
                }

                string expr = NormalizeExpression(equation.Text);

                var exprA = new NCalc.Expression(NormalizeExpression(start.Text));
                exprA.Parameters["PI"] = Math.PI;
                double a = Convert.ToDouble(exprA.Evaluate());

                var exprB = new NCalc.Expression(NormalizeExpression(end.Text));
                exprB.Parameters["PI"] = Math.PI;
                double b = Convert.ToDouble(exprB.Evaluate());

                double epsilon = double.Parse(eps.Text.Replace(',', '.'), CultureInfo.InvariantCulture);

                // Кількість знаків після коми
                int decimals = GetDecimalsFromEpsilon(epsilon);

                res.Text = "=== ПОЧАТОК ОБЧИСЛЕНЬ ===\n";
                res.Text += $"Рівняння: {expr}\n";
                res.Text += $"Проміжок: [{a}, {b}]\n";
                res.Text += $"Точність: {epsilon}\n\n";

                res.Text += "Етап 1: Відділення коренів...\n";
                var intervals = SeparateRoots(expr, a, b, epsilon, decimals);

                if (intervals.Count == 0)
                {
                    res.Text += "Результат: Корені не знайдені на цьому проміжку";
                    return;
                }

                res.Text += $"Знайдено {intervals.Count} інтервал(ів) з коренями:\n";
                intervals = intervals.OrderBy(i => i.Item1).ToList();
                for (int i = 0; i < intervals.Count; i++)
                {
                    res.Text += $"  [{intervals[i].Item1.ToString($"F{decimals}")}, {intervals[i].Item2.ToString($"F{decimals}")}]\n";
                }
                res.Text += "\n";

                List<double> roots = new List<double>();

                res.Text += "Етап 2: Уточнення коренів методом січних...\n\n";

                for (int i = 0; i < intervals.Count; i++)
                {
                    var interval = intervals[i];
                    res.Text += $"Інтервал {i + 1}: [{interval.Item1.ToString($"F{decimals}")}, {interval.Item2.ToString($"F{decimals}")}]\n";

                    res.Text += "  Застосування методу січних...\n";
                    double root = Secant(expr, interval.Item1, interval.Item2, epsilon, decimals);

                    if (Math.Abs(root) < epsilon)
                        root = 0;

                    if (!roots.Any(r => Math.Abs(r - root) < epsilon))
                    {
                        roots.Add(root);
                        res.Text += $"  Знайдено корінь: {root.ToString($"F{decimals}")}\n\n";
                    }
                    else
                    {
                        res.Text += $"  Корінь {root.ToString($"F{decimals}")} вже знайдений (дублікат)\n\n";
                    }
                }

                res.Text += "=== РЕЗУЛЬТАТИ ===\n";
                res.Text += $"Знайдено унікальних коренів: {roots.Count}\n";
                res.Text += "Корені: " + string.Join("; ", roots.Select(r => Math.Round(r, decimals))) + "\n";
                DrawGraph(expr, a, b, roots);
            }
            catch (Exception ex)
            {
                res.Text = "Помилка: " + ex.Message;
            }
        }
        // Визначаємо кількість десяткових знаків на основі епсилон
        private int GetDecimalsFromEpsilon(double epsilon)
        {
            return Math.Min(15, Math.Max(0, (int)Math.Ceiling(-Math.Log10(epsilon))));
        }
        // Відділення коренів методом послідовного поділу інтервалу
        List<(double, double)> SeparateRoots(string expr, double a, double b, double eps, int decimals)
        {
            List<(double, double)> result = new List<(double, double)>();
            Queue<(double left, double right)> queue = new Queue<(double, double)>();

            queue.Enqueue((a, b));
            int intervalNumber = 0;

            res.Text += $"Початковий: [{a.ToString($"F{decimals}")}, {b.ToString($"F{decimals}")}]\n\n";

            while (queue.Count > 0)
            {
                var interval = queue.Dequeue();
                double start = interval.Item1;
                double end = interval.Item2;

                intervalNumber++;
                res.Text += $"#{intervalNumber}: [{start.ToString($"F{decimals}")}, {end.ToString($"F{decimals}")}] → 10 під-інтервалів\n";

                double step = (end - start) / 10.0;
                int foundInThisInterval = 0;

                for (int i = 0; i < 10; i++)
                {
                    double left = start + i * step;
                    double right = left + step;

                    res.Text += $"  {i + 1}: [{left.ToString($"F{decimals}")},{right.ToString($"F{decimals}")}] ";

                    string derivativeInfo;
                    bool derivChange = DerivativeChangesSign(expr, left, right, out derivativeInfo, decimals);
                    bool signChange = FunctionChangesSign(expr, left, right);

                    if (derivChange)
                    {
                        res.Text += derivativeInfo;

                        if ((right - left) > eps)
                        {
                            res.Text += $" → Додано до черги\n";
                            queue.Enqueue((left, right));
                        }
                        else
                        {
                            // Інтервал звузився до мінімуму. Перевіримо значення функції в центрі.
                            double mid = (left + right) / 2.0;
                            double fMid = SolveFunction(expr, mid);

                            // Якщо в екстремумі функція близька до нуля - це корінь (дотик)
                            if (Math.Abs(fMid) <= eps)
                            {
                                res.Text += $" → Знайдено корінь дотику! Відправлено на уточнення\n";
                                result.Add((left, right));
                                foundInThisInterval++;
                            }
                            else
                            {
                                res.Text += $" → Відкинуто (екстремум не на осі)\n";
                            }
                        }
                    }
                    else
                    {
                        res.Text += derivativeInfo;
                        
                        if (signChange)
                        {
                            res.Text += $"      → КОРІНЬ! Відправлено на уточнення\n";
                            result.Add((left, right));
                            foundInThisInterval++;
                        }
                        else
                        {
                            res.Text += $"      → Відкинуто\n";
                        }
                    }
                }

                res.Text += $"  Знайдено: {foundInThisInterval}, Черга: {queue.Count}\n\n";
            }

            res.Text += $"Оброблено {intervalNumber} інтервалів, знайдено {result.Count} з коренями\n\n";

            return result;
        }
        // Перевіряємо зміну знака похідної на інтервалі
        bool DerivativeChangesSign(string expr, double a, double b, out string derivativeInfo, int decimals)
        {
            int points = 5;
            double step = (b - a) / (points - 1);

            StringBuilder info = new StringBuilder();
            info.Append($"      Похідні: ");

            double prev = Derivative(expr, a);
            List<string> derivatives = new List<string> { $"x={a.ToString($"F{decimals}")}: f'={prev.ToString($"F{decimals}")}" };

            bool signChanged = false;

            for (int i = 1; i < points; i++)
            {
                double x = a + i * step;
                double cur = Derivative(expr, x);
                derivatives.Add($"x={x.ToString($"F{decimals}")}:f'={cur.ToString($"F{decimals}")}");

                if (prev * cur < 0)
                {
                    signChanged = true;
                }

                prev = cur;
            }

            info.Append(string.Join("; ", derivatives));
            info.Append(signChanged ? $" → Знак змінився\n" : $" → Знак {(prev > 0 ? "+" : "-")}\n");

            derivativeInfo = info.ToString();
            return signChanged;
        }
        // Перевіряємо зміну знака функції на інтервалі
        bool FunctionChangesSign(string expr, double a, double b)
        {
            double f1 = SolveFunction(expr, a);
            double f2 = SolveFunction(expr, b);

            return f1 * f2 <= 0;
        }
        // Чисельне наближення похідної в точці x
        double Derivative(string expr, double x)
        {
            double dx = 0.001;
            return (SolveFunction(expr, x + dx) - SolveFunction(expr, x)) / dx;
        }
        // Метод січних для уточнення кореня на інтервалі [x0, x1]
        double Secant(string expr, double x0, double x1, double eps, int decimals)
        {
            double f0 = SolveFunction(expr, x0);
            double f1 = SolveFunction(expr, x1);
            int iteration = 0;

            //while (Math.Abs(f1) > eps)
            while (Math.Abs(x1-x0) > eps)
            {
                iteration++;
                double x2 = x1 - f1 * (x1 - x0) / (f1 - f0);

                res.Text += $"    #{iteration}: x={x2.ToString($"F{decimals}")}, f(x)={f1.ToString($"F{decimals}")}; ";

                x0 = x1;
                f0 = f1;

                x1 = x2;
                f1 = SolveFunction(expr, x1);
            }

            res.Text += $"#{iteration}: x={x1.ToString($"F{decimals}")}, f(x)={f1.ToString($"F{decimals}")}";

            res.Text += $"\n    Збіжність за {iteration} ітерацій, |f(x)|={Math.Abs(f1).ToString($"F{decimals}")}\n";

            return x1;
        }


        // Функціонал програми для обробки введення
        string NormalizeExpression(string expr)
        {
            expr = expr.Replace(" ", "");

            // e^x → Exp(x), e^2 → Exp(2), e^(x+1) → Exp(x+1)
            expr = Regex.Replace(expr, @"e\^(\w+|\([^)]*\)|\-?\d+\.?\d*)", "Exp($1)");

            // e → Exp(1)
            expr = Regex.Replace(expr, @"(?<![a-zA-Z])e(?![a-zA-Z0-9\^])", "Exp(1)");

            // x^2, (x+1)^3, x^(-2), sin^2, etc. → Pow(base,exponent)
            expr = Regex.Replace(expr, @"(\w+|\([^)]*\))\^(\-?\d+\.?\d*|\([^)]*\))", "Pow($1,$2)");

            // 2x → 2*x
            expr = Regex.Replace(expr, @"(\d)x", "$1*x");

            // )x → )*x
            expr = Regex.Replace(expr, @"\)x", ")*x");

            // x( → x*(
            expr = Regex.Replace(expr, @"x\(", "x*(");

            // )( → )*(
            expr = Regex.Replace(expr, @"\)\(", ")*(");

            return expr;
        }

        private void InsertTextAtCursor(string text)
        {
            if (activeBox != null)
            {
                int caretIndex = activeBox.CaretIndex;
                activeBox.Text = activeBox.Text.Insert(caretIndex, text);
                activeBox.CaretIndex = caretIndex + text.Length;
                activeBox.Focus();
            }
        }

        private char? GetCharBeforeCursor()
        {
            if (activeBox != null && activeBox.CaretIndex > 0)
            {
                return activeBox.Text[activeBox.CaretIndex - 1];
            }
            return null;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (activeBox != null)
            {
                activeBox.Text = "";
            }
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (activeBox != null && activeBox.CaretIndex > 0)
            {
                int caretIndex = activeBox.CaretIndex;
                activeBox.Text = activeBox.Text.Remove(caretIndex - 1, 1);
                activeBox.CaretIndex = caretIndex - 1;
                activeBox.Focus();
            }
        }

        private void AddText_Click(object sender, RoutedEventArgs e)
        {
            InsertTextAtCursor((sender as Button)!.Content.ToString());
        }

        private void Special_Click(object sender, RoutedEventArgs e)
        {
            if (activeBox != null)
            {
                char? prevChar = GetCharBeforeCursor();
                if (prevChar == '.')
                    return;
            
                InsertTextAtCursor((sender as Button)!.Content.ToString());
            }
        }

        private void Operator_Click(object sender, RoutedEventArgs e)
        {
            if (activeBox != null)
            {
                if (activeBox.CaretIndex == 0)
                    return;
                
                char? prevChar = GetCharBeforeCursor();
                if (prevChar.HasValue && "+-*/.".Contains(prevChar.Value))
                    return;

                InsertTextAtCursor((sender as Button)!.Content.ToString());
            }
        }

        private void Minus_Click(object sender, RoutedEventArgs e)
        {
            if (activeBox != null)
            {
                char? prevChar = GetCharBeforeCursor();
                if (prevChar.HasValue && "+-*/.".Contains(prevChar.Value))
                    return;

                InsertTextAtCursor((sender as Button)!.Content.ToString());
            }
        }

        private void Function_Click(object sender, RoutedEventArgs e)
        {
            string func = (sender as Button)!.Content.ToString()!;
            InsertTextAtCursor(func + "(");
        }
        private void Log_Click(object sender, RoutedEventArgs e)
        {
            InsertTextAtCursor("Log(,");
        }


        private void CloseBracket_Click(object sender, RoutedEventArgs e)
        {
            if (activeBox != null)
            {
                char? prevChar = GetCharBeforeCursor();
                if (prevChar.HasValue && !"+-*/(.".Contains(prevChar.Value))
                {
                    InsertTextAtCursor(")");
                }
            }
        }

        private void Point_Click(object sender, RoutedEventArgs e)
        {
            if (activeBox != null)
            {
                if (activeBox.CaretIndex == 0)
                    return;
                
                char? prevChar = GetCharBeforeCursor();
                if (prevChar.HasValue && "+-*/.".Contains(prevChar.Value))
                    return;
                    
                InsertTextAtCursor(".");
            }
        }

        private void Arrow_Click(object sender, RoutedEventArgs e)
        {
            if (activeBox != null && sender is Button btn)
            {
                string direction = btn.Content?.ToString();

                // Move cursor left
                if (direction == "←" && activeBox.CaretIndex > 0)
                {
                    activeBox.CaretIndex--;
                }
                // Move cursor right
                else if (direction == "→" && activeBox.CaretIndex < activeBox.Text.Length)
                {
                    activeBox.CaretIndex++;
                }

                // Return focus to the TextBox so the user can see the blinking cursor
                activeBox.Focus();
            }
        }

        TextBox activeBox = null;
        private void TextBox_Select(object sender, MouseButtonEventArgs e)
        {
            activeBox = sender as TextBox;
        }

        void DrawGraph(string expr, double a, double b, List<double> roots)
        {
            var model = new PlotModel 
            { 
                Title = "Графік функції",
                PlotType = PlotType.Cartesian // Forces a 1:1 scale on the axes
            };

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "x",
                Minimum = a,
                Maximum = b,
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "f(x)",
                Minimum = -10,
                Maximum = 10
            });

            var series = new LineSeries
            {
                Title = "f(x)",
                StrokeThickness = 2,
                Color = OxyColors.Purple
            };

            int points = 1000;
            double step = (b - a) / points;

            for (int i = 0; i <= points; i++)
            {
                double x = a + i * step;
                double y = SolveFunction(expr, x);

                if (!double.IsNaN(y) && !double.IsInfinity(y))
                {
                    series.Points.Add(new DataPoint(x, y));
                }
            }

            model.Series.Add(series);
            
            var rootSeries = new ScatterSeries
            {
                Title = "Корені",
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Purple
            };

            foreach (double root in roots)
            {
                rootSeries.Points.Add(new ScatterPoint(root, 0));
            }

            model.Series.Add(rootSeries);
            model.Annotations.Add(new LineAnnotation
            {
                Y = 0,
                Type = LineAnnotationType.Horizontal,
                Color = OxyColors.Gray
            });
            model.Annotations.Add(new LineAnnotation
            {
                X = 0,
                Type = LineAnnotationType.Vertical,
                Color = OxyColors.Gray
            });
            plotView.Model = model;
        }
    }
}