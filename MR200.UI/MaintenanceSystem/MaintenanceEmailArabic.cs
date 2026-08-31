using System.Globalization;
using System.Text;
using DataAccess.Entities;

namespace MR200.UI.MaintenanceSystem
{
    /// <summary>
    /// Renders the Arabic half of the maintenance alert.
    ///
    /// This is appended after the complete English alert - the English content is never
    /// modified or replaced. The Arabic block is laid out right-to-left and carries the
    /// same figures, so the maintenance department can read either version.
    ///
    /// Numbers, dates and money stay in Western digits deliberately: they are
    /// engineering values that must match the English half exactly.
    /// </summary>
    internal static class MaintenanceEmailArabic
    {
        private const string PrimaryDark = "#17463E";
        private const string DeepTeal = "#1F5F5B";
        private const string Accent = "#E05C1A";
        private const string Gold = "#C89B3C";
        private const string DangerRed = "#EF4444";
        private const string LightBg = "#F5F7FA";
        private const string DarkText = "#1A1A2E";
        private const string SubText = "#6B7280";
        private const string Border = "#E5E7EB";

        private const string ArabicFont =
            "'Segoe UI', Tahoma, 'Traditional Arabic', 'Arabic Typesetting', Arial, sans-serif";

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>Arabic label for an element's life unit.</summary>
        private static string UnitLabel(string englishUnit) =>
            englishUnit == "passes" ? "تمريرة" : "دورة";

        public static void Append(StringBuilder sb, MaintenanceAlertContext context,
            bool hasElementImage, bool hasMachinePositionImage)
        {
            var element = context.FailedElement;
            string unit = UnitLabel(element.UnitLabel);
            double usedPercent = element.DefaultLife <= 0
                ? 0
                : context.ConsumedLifeAtFailure / element.DefaultLife * 100.0;

            AppendDivider(sb);

            // Everything below sits inside one RTL container.
            sb.Append($"<tr><td dir=\"rtl\" style=\"direction:rtl;text-align:right;font-family:{ArabicFont};\">");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" " +
                      "style=\"direction:rtl;text-align:right;\">");

            AppendHeader(sb, context);
            AppendIntro(sb, context, unit, usedPercent);
            AppendFailedElement(sb, context, unit, usedPercent);
            AppendMachineState(sb, context);
            AppendRecentMaintenance(sb, context);
            AppendApproachingFailure(sb, context);
            AppendLastGeneralMaintenance(sb, context);
            AppendAttachments(sb, context, hasElementImage);
            AppendFooter(sb);

            sb.Append("</table></td></tr>");
        }

        private static void AppendDivider(StringBuilder sb)
        {
            sb.Append("<tr><td style=\"padding:10px 30px 0 30px;\">");
            sb.Append($"<div style=\"border-top:2px dashed {Border};margin-top:10px;\"></div>");
            sb.Append($"<div style=\"text-align:center;margin-top:-11px;\">" +
                      $"<span style=\"background:#FFFFFF;padding:0 14px;font-size:11px;color:{SubText};" +
                      $"letter-spacing:1px;\">ARABIC VERSION &middot; النسخة العربية</span></div>");
            sb.Append("</td></tr>");
        }

        private static void AppendHeader(StringBuilder sb, MaintenanceAlertContext context)
        {
            sb.Append($"<tr><td style=\"background-color:{PrimaryDark};padding:24px 30px;margin-top:14px;\">");
            sb.Append($"<div style=\"display:inline-block;background:{DangerRed};color:#FFFFFF;font-size:12px;" +
                      "font-weight:bold;padding:6px 14px;border-radius:20px;\">تنبيه صيانة</div>");
            sb.Append("<div style=\"color:#FFFFFF;font-size:24px;font-weight:bold;margin-top:12px;\">" +
                      "آلة التقطيع MR200</div>");
            sb.Append($"<div style=\"color:#9FD3CB;font-size:13px;margin-top:5px;\">إيقاف تلقائي &middot; " +
                      $"{Escape(context.MachineState.FailureDetectedAt.ToString("yyyy-MM-dd HH:mm:ss", Culture))}</div>");
            sb.Append("</td></tr>");
        }

        private static void AppendIntro(StringBuilder sb, MaintenanceAlertContext context,
            string unit, double usedPercent)
        {
            var element = context.FailedElement;
            sb.Append("<tr><td style=\"padding:24px 30px 6px 30px;\">");
            sb.Append($"<p style=\"margin:0 0 12px 0;font-size:15px;color:{DarkText};\">" +
                      "إلى قسم صيانة خط الإنتاج المحترم،</p>");
            sb.Append($"<p style=\"margin:0;font-size:14px;line-height:1.9;color:{SubText};\">" +
                      "توقفت الآلة تلقائياً لأن العنصر التالي قد بلغ نهاية عمره المتوقع. " +
                      $"<strong style=\"color:{DarkText};\">{Escape(element.TypeName)}</strong> " +
                      $"في الموقع رقم <strong style=\"color:{DarkText};\">{element.OrderOfElementAtMachine}</strong> " +
                      $"استهلك <strong style=\"color:{Accent};\">{Number(context.ConsumedLifeAtFailure)} {unit}</strong> " +
                      $"من أصل عمر افتراضي مقداره {Number(element.DefaultLife)} {unit} " +
                      $"(أي ما نسبته {Percent(usedPercent)} من عمره المتوقع).</p>");
            sb.Append("</td></tr>");
        }

        private static void AppendFailedElement(StringBuilder sb, MaintenanceAlertContext context,
            string unit, double usedPercent)
        {
            var element = context.FailedElement;

            AppendSectionTitle(sb, "العنصر المعطّل", Accent);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");
            sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" " +
                      $"style=\"border:1px solid {Border};border-radius:10px;border-collapse:separate;overflow:hidden;\">");

            AppendKeyValue(sb, "العنصر", $"{Escape(element.TypeName)} — {element.OrderOfElementAtMachine}", true);
            AppendKeyValue(sb, "نوع العنصر", Escape(element.TypeName), false);
            AppendKeyValue(sb, "رقم التعريف / الترتيب في الآلة", element.OrderOfElementAtMachine.ToString(), true);
            AppendKeyValue(sb, "الموقع في الآلة", Escape(ArabicPosition(element.OrderOfElementAtMachine)), false);
            AppendKeyValue(sb, "العمر الافتراضي", $"{Number(element.DefaultLife)} {unit}", true);
            AppendKeyValue(sb, "العمر المستهلك عند العطل",
                $"<span style=\"color:{DangerRed};font-weight:bold;\">{Number(context.ConsumedLifeAtFailure)} {unit}</span>", false);
            AppendKeyValue(sb, "العمر المتبقي",
                $"{Number(Math.Max(0, element.DefaultLife - context.ConsumedLifeAtFailure))} {unit}", true);
            AppendKeyValue(sb, "نسبة العمر المستهلك", $"<strong>{Percent(usedPercent)}</strong>", false);
            AppendKeyValue(sb, "سعر العنصر", Money(element.Price), true);

            sb.Append("</table></td></tr>");
        }

        private static void AppendMachineState(StringBuilder sb, MaintenanceAlertContext context)
        {
            var state = context.MachineState;

            AppendSectionTitle(sb, "حالة الآلة عند حدوث العطل", PrimaryDark);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");
            sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" " +
                      $"style=\"border:1px solid {Border};border-radius:10px;border-collapse:separate;overflow:hidden;\">");

            AppendKeyValue(sb, "حالة الآلة",
                $"<span style=\"color:{DangerRed};font-weight:bold;\">متوقفة — إيقاف تلقائي للصيانة</span>", true);
            AppendKeyValue(sb, "حالة الإنتاج / القطع", "توقفت عملية القطع بسبب بلوغ العنصر نهاية عمره", false);
            AppendKeyValue(sb, "مدة تشغيل الآلة", Escape(state.MachineRuntime), true);
            AppendKeyValue(sb, "كمية الإنتاج منذ بدء التشغيل",
                $"{state.ProductionQuantityInCubicMeter.ToString("F4", Culture)} م³", false);
            AppendKeyValue(sb, "الطاقة الكهربائية المستهلكة منذ بدء التشغيل",
                $"{state.ConsumedElectricity.ToString("F4", Culture)} ك.و.س", true);
            AppendKeyValue(sb, "سرعة الآلة الحالية",
                $"{state.MachineSpeedInRPM.ToString("F1", Culture)} دورة/دقيقة", false);
            AppendKeyValue(sb, "نوع الخشب قيد المعالجة", Escape(state.WoodType), true);

            sb.Append("</table></td></tr>");
        }

        private static void AppendRecentMaintenance(StringBuilder sb, MaintenanceAlertContext context)
        {
            var records = context.FailedElementRecentMaintenance;
            int days = context.MaintenanceHistoryWindowInDays;

            AppendSectionTitle(sb, $"صيانة العنصر المعطّل خلال آخر {days} يوماً", Gold);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");
            sb.Append($"<p style=\"margin:0 0 10px 0;font-size:13px;color:{SubText};\">" +
                      $"عدد عمليات الصيانة خلال آخر {days} يوماً: " +
                      $"<strong style=\"color:{DarkText};font-size:15px;\">{records.Count}</strong></p>");

            if (records.Count == 0)
            {
                AppendEmptyNotice(sb, "لم تُنفَّذ أي عملية صيانة على هذا العنصر خلال هذه الفترة.");
            }
            else
            {
                sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" " +
                          $"style=\"border-collapse:collapse;font-size:12.5px;border:1px solid {Border};" +
                          "border-radius:10px;overflow:hidden;\">");
                AppendTableHeader(sb, Gold, "تاريخ الصيانة", "نُفِّذت بواسطة", "تكلفة الصيانة", "سعر العنصر", "تكلفة زمن التوقف");

                int index = 0;
                foreach (var record in records)
                {
                    AppendTableRow(sb, index++ % 2 == 1,
                        Escape(record.MaintenanceDate.ToString("yyyy-MM-dd HH:mm", Culture)),
                        Escape(record.DoneBy),
                        Money(record.Cost),
                        Money(record.ElementPrice),
                        Money(record.StoppingTimeCost));
                }
                sb.Append("</table>");
            }

            sb.Append("</td></tr>");
        }

        private static void AppendApproachingFailure(StringBuilder sb, MaintenanceAlertContext context)
        {
            var elements = context.ElementsApproachingFailure;

            AppendSectionTitle(sb, "عناصر أخرى تقترب من نهاية عمرها", DangerRed);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");
            sb.Append($"<p style=\"margin:0 0 10px 0;font-size:12.5px;color:{SubText};\">" +
                      $"العناصر التي بلغ عمرها المستهلك {Percent(context.WarningThresholdPercent)} أو أكثر من عمرها المتوقع.</p>");

            if (elements.Count == 0)
            {
                AppendEmptyNotice(sb, $"لا يوجد عنصر آخر تجاوز حد التحذير البالغ {Percent(context.WarningThresholdPercent)}.");
            }
            else
            {
                sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" " +
                          $"style=\"border-collapse:collapse;font-size:12.5px;border:1px solid {Border};" +
                          "border-radius:10px;overflow:hidden;\">");
                AppendTableHeader(sb, DangerRed, "العنصر", "الموقع", "العمر المستهلك", "العمر الافتراضي", "العمر المتبقي", "النسبة المستهلكة");

                int index = 0;
                foreach (var item in elements)
                {
                    double percent = item.LifeUsedPercentage;
                    string colour = percent >= MaintenanceSettings.CriticalThresholdPercent ? DangerRed : Gold;
                    AppendTableRow(sb, index++ % 2 == 1,
                        $"{Escape(item.TypeName)} — {item.OrderOfElementAtMachine}",
                        Escape(ArabicPosition(item.OrderOfElementAtMachine)),
                        Number(item.TotalConsumedLife),
                        Number(item.DefaultLife),
                        Number(item.RemainingLife),
                        $"<span style=\"color:{colour};font-weight:bold;\">{Percent(percent)}</span>");
                }
                sb.Append("</table>");
            }

            sb.Append("</td></tr>");
        }

        private static void AppendLastGeneralMaintenance(StringBuilder sb, MaintenanceAlertContext context)
        {
            AppendSectionTitle(sb, "آخر صيانة عامة للآلة", DeepTeal);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");

            var last = context.LastMachineMaintenance;
            if (last == null)
            {
                AppendEmptyNotice(sb, "لا يوجد سجل صيانة سابق.");
            }
            else
            {
                string maintained = last.Element?.ElementInformation != null
                    ? $"{Escape(last.Element.ElementInformation.Name)} — {last.Element.OrderOfElementAtMachine}"
                    : "غير متوفر";

                sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" " +
                          $"style=\"border:1px solid {Border};border-radius:10px;border-collapse:separate;overflow:hidden;\">");
                AppendKeyValue(sb, "تاريخ آخر صيانة",
                    Escape(last.MaintenanceDate.ToString("yyyy-MM-dd HH:mm", Culture)), true);
                AppendKeyValue(sb, "العنصر الذي تمت صيانته", maintained, false);
                AppendKeyValue(sb, "نُفِّذت بواسطة", Escape(last.DoneBy), true);
                AppendKeyValue(sb, "تكلفة الصيانة", Money(last.Cost), false);
                AppendKeyValue(sb, "سعر العنصر", Money(last.ElementPrice), true);
                AppendKeyValue(sb, "تكلفة زمن التوقف", Money(last.StoppingTimeCost), false);
                sb.Append("</table>");
            }

            sb.Append("</td></tr>");
        }

        private static void AppendAttachments(StringBuilder sb, MaintenanceAlertContext context, bool hasElementImage)
        {
            bool hasCatalog = !string.IsNullOrWhiteSpace(context.FailedElement.CatalogFileName);

            AppendSectionTitle(sb, "المرفقات", SubText);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");
            sb.Append($"<ol style=\"margin:0;padding-right:20px;padding-left:0;font-size:13px;color:{SubText};line-height:1.9;\">");
            sb.Append(hasElementImage
                ? "<li>صورة العنصر المعطّل</li>"
                : "<li>صورة العنصر المعطّل &mdash; <em>غير متوفرة</em></li>");
            sb.Append(hasCatalog
                ? $"<li>كتالوج العنصر &mdash; {Escape(context.FailedElement.CatalogFileName!)}</li>"
                : "<li><em>كتالوج العنصر غير متوفر</em></li>");
            sb.Append("</ol></td></tr>");
        }

        private static void AppendFooter(StringBuilder sb)
        {
            sb.Append("<tr><td style=\"padding:22px 30px 8px 30px;\">");
            sb.Append($"<div style=\"background:#FEF2F2;border-right:4px solid {DangerRed};border-radius:8px 0 0 8px;" +
                      $"padding:14px 16px;font-size:13px;line-height:1.9;color:{DarkText};\">" +
                      "تم إيقاف الآلة تلقائياً لتمكين قسم الصيانة من فحص العنصر المتأثر. " +
                      "لا يمكن استئناف الإنتاج بأمان قبل فحص العنصر أو استبداله.</div>");
            sb.Append("</td></tr>");

            sb.Append($"<tr><td style=\"padding:20px 30px 26px 30px;border-top:1px solid {Border};\">");
            sb.Append($"<div style=\"font-size:13px;color:{DarkText};\">مع خالص التحية،</div>");
            sb.Append($"<div style=\"font-size:13px;font-weight:bold;color:{PrimaryDark};margin-top:3px;\">" +
                      "نظام مراقبة خط الإنتاج</div>");
            sb.Append($"<div style=\"font-size:11px;color:{SubText};margin-top:10px;\">" +
                      "أُنشئت هذه الرسالة تلقائياً بواسطة نظام الصيانة التنبؤية للآلة MR200. الرجاء عدم الرد عليها.</div>");
            sb.Append("</td></tr>");
        }

        // ------------------------------------------------------------- helpers

        /// <summary>
        /// Arabic description of a machine position, derived from the same 1-16 numbering
        /// the English half uses.
        /// </summary>
        private static string ArabicPosition(int order) => order switch
        {
            1 => "عمود التغذية الأول من اليمين — المحمل الطرفي",
            2 => "عمود التغذية الأول من اليمين — المحمل المجاور",
            3 => "عمود التغذية الثاني من اليمين — المحمل الطرفي",
            4 => "عمود التغذية الثاني من اليمين — المحمل المجاور",
            5 => "عمود التغذية الثالث من اليمين — المحمل الطرفي",
            6 => "عمود التغذية الثالث من اليمين — المحمل المجاور",
            7 => "عمود التغذية الرابع من اليمين — المحمل الطرفي",
            8 => "عمود التغذية الرابع من اليمين — المحمل المجاور",
            9 => "عمود القطع العلوي — محمل 16007",
            10 => "عمود القطع العلوي — محمل 16008",
            11 => "عمود القطع السفلي — محمل 16007",
            12 => "عمود القطع السفلي — محمل 16008",
            13 => "سير يدير عمود التغذية الأول من اليمين",
            14 => "سير يدير عمود التغذية الثاني من اليمين",
            15 => "سير يدير عمود التغذية الثالث من اليمين",
            16 => "سير يدير عمود التغذية الرابع من اليمين",
            _ => "غير محدد"
        };

        private static void AppendSectionTitle(StringBuilder sb, string title, string colour)
        {
            sb.Append("<tr><td style=\"padding:24px 30px 12px 30px;\">");
            sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"direction:rtl;\"><tr>");
            sb.Append($"<td style=\"width:4px;background:{colour};border-radius:2px;\">&nbsp;</td>");
            sb.Append($"<td style=\"padding-right:10px;font-size:13.5px;font-weight:bold;color:{colour};\">{Escape(title)}</td>");
            sb.Append("</tr></table></td></tr>");
        }

        private static void AppendKeyValue(StringBuilder sb, string key, string valueHtml, bool shaded)
        {
            string background = shaded ? "#FAFBFC" : "#FFFFFF";
            sb.Append($"<tr style=\"background:{background};\">");
            sb.Append($"<td align=\"right\" style=\"padding:9px 14px;font-size:12.5px;color:{SubText};width:46%;" +
                      $"border-bottom:1px solid {Border};\">{Escape(key)}</td>");
            sb.Append($"<td align=\"right\" style=\"padding:9px 14px;font-size:12.5px;color:{DarkText};font-weight:600;" +
                      $"border-bottom:1px solid {Border};\">{valueHtml}</td>");
            sb.Append("</tr>");
        }

        private static void AppendTableHeader(StringBuilder sb, string colour, params string[] headers)
        {
            sb.Append($"<tr style=\"background:{colour};\">");
            foreach (var header in headers)
                sb.Append($"<th align=\"right\" style=\"padding:9px 12px;color:#FFFFFF;font-size:11.5px;" +
                          $"font-weight:600;\">{Escape(header)}</th>");
            sb.Append("</tr>");
        }

        private static void AppendTableRow(StringBuilder sb, bool shaded, params string[] cells)
        {
            string background = shaded ? "#FAFBFC" : "#FFFFFF";
            sb.Append($"<tr style=\"background:{background};\">");
            foreach (var cell in cells)
                sb.Append($"<td align=\"right\" style=\"padding:9px 12px;border-top:1px solid {Border};" +
                          $"color:{DarkText};\">{cell}</td>");
            sb.Append("</tr>");
        }

        private static void AppendEmptyNotice(StringBuilder sb, string message)
        {
            sb.Append($"<div style=\"background:{LightBg};border:1px dashed {Border};border-radius:10px;" +
                      $"padding:16px;font-size:13px;color:{SubText};text-align:center;\">{Escape(message)}</div>");
        }

        private static string Number(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "غير متوفر" : value.ToString("N0", Culture);

        private static string Percent(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "غير متوفر" : value.ToString("F2", Culture) + "%";

        private static string Money(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "غير متوفر" : "$" + value.ToString("N2", Culture);

        private static string Escape(string? value) =>
            string.IsNullOrEmpty(value) ? "" : System.Net.WebUtility.HtmlEncode(value);
    }
}
