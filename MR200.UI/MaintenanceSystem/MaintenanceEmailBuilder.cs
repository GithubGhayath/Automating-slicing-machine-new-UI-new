using System.Globalization;
using System.Text;
using DataAccess.Entities;

namespace MR200.UI.MaintenanceSystem
{
    /// <summary>
    /// Renders the maintenance alert as an HTML e-mail, in the same colour language
    /// the application uses on screen.
    ///
    /// Everything is inline-styled and table-based, because mail clients strip
    /// stylesheets and ignore modern layout.
    /// </summary>
    public static class MaintenanceEmailBuilder
    {
        private const string PrimaryDark = "#17463E";
        private const string DeepTeal = "#1F5F5B";
        private const string Accent = "#E05C1A";
        private const string Gold = "#C89B3C";
        private const string DangerRed = "#EF4444";
        private const string SuccessGreen = "#10B981";
        private const string LightBg = "#F5F7FA";
        private const string DarkText = "#1A1A2E";
        private const string SubText = "#6B7280";
        private const string Border = "#E5E7EB";

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>Content id used to embed the element picture inside the body.</summary>
        public const string ElementImageContentId = "failed-element-image";

        /// <summary>Content id used to embed the machine-position picture inside the body.</summary>
        public const string MachinePositionImageContentId = "failed-element-position-image";

        public static string BuildSubject(MaintenanceAlertContext context)
        {
            var element = context.FailedElement;
            return $"MAINTENANCE ALERT - {element.TypeName} #{element.OrderOfElementAtMachine} reached end of life - MR200 stopped"
                   + $" | تنبيه صيانة - {element.TypeName} رقم {element.OrderOfElementAtMachine} بلغ نهاية عمره - توقفت MR200";
        }

        public static string BuildHtmlBody(MaintenanceAlertContext context,
            bool hasElementImage, bool hasMachinePositionImage)
        {
            var element = context.FailedElement;
            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"></head>");
            sb.Append($"<body style=\"margin:0;padding:0;background:{LightBg};" +
                      "font-family:Segoe UI,Helvetica,Arial,sans-serif;color:" + DarkText + ";\">");

            sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{LightBg};padding:24px 12px;\"><tr><td align=\"center\">");
            sb.Append("<table role=\"presentation\" width=\"680\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:680px;max-width:100%;background:#FFFFFF;border-radius:14px;overflow:hidden;box-shadow:0 2px 10px rgba(0,0,0,0.07);\">");

            AppendHeader(sb, context);
            AppendIntro(sb, context);
            AppendFailedElement(sb, context, hasElementImage, hasMachinePositionImage);
            AppendMachineState(sb, context);
            AppendRecentMaintenance(sb, context);
            AppendApproachingFailure(sb, context);
            AppendLastGeneralMaintenance(sb, context);
            AppendAttachmentsNote(sb, context, hasElementImage);
            AppendFooter(sb);

            // The Arabic version is appended after the English one. The English content
            // above is never altered.
            MaintenanceEmailArabic.Append(sb, context, hasElementImage, hasMachinePositionImage);

            sb.Append("</table></td></tr></table></body></html>");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- header

        private static void AppendHeader(StringBuilder sb, MaintenanceAlertContext context)
        {
            sb.Append($"<tr><td style=\"background:linear-gradient(135deg,{PrimaryDark} 0%,{DeepTeal} 100%);background-color:{PrimaryDark};padding:26px 30px;\">");
            sb.Append($"<div style=\"display:inline-block;background:{DangerRed};color:#FFFFFF;font-size:11px;font-weight:bold;letter-spacing:1.4px;padding:6px 12px;border-radius:20px;\">MAINTENANCE ALERT</div>");
            sb.Append("<div style=\"color:#FFFFFF;font-size:25px;font-weight:bold;margin-top:14px;\">MR200 Slicing Machine</div>");
            sb.Append($"<div style=\"color:#9FD3CB;font-size:13px;margin-top:5px;\">Automatic stop &middot; {Escape(context.MachineState.FailureDetectedAt.ToString("dddd dd MMMM yyyy, HH:mm:ss", Culture))}</div>");
            sb.Append("</td></tr>");
        }

        private static void AppendIntro(StringBuilder sb, MaintenanceAlertContext context)
        {
            var element = context.FailedElement;
            sb.Append("<tr><td style=\"padding:26px 30px 6px 30px;\">");
            sb.Append($"<p style=\"margin:0 0 12px 0;font-size:15px;color:{DarkText};\">Dear Production Line Maintenance Department,</p>");
            sb.Append($"<p style=\"margin:0;font-size:14px;line-height:1.65;color:{SubText};\">The machine has automatically stopped because the following component has reached its expected life. " +
                      $"<strong style=\"color:{DarkText};\">{Escape(element.TypeName)}</strong> at machine position " +
                      $"<strong style=\"color:{DarkText};\">#{element.OrderOfElementAtMachine}</strong> consumed " +
                      $"<strong style=\"color:{Accent};\">{Number(context.ConsumedLifeAtFailure)} {Escape(element.UnitLabel)}</strong> " +
                      $"against a rated life of {Number(element.DefaultLife)} {Escape(element.UnitLabel)} " +
                      $"({Percent(LifeUsedPercent(context))} of its expected life).</p>");
            sb.Append("</td></tr>");
        }

        // -------------------------------------------------------- failed element

        private static void AppendFailedElement(StringBuilder sb, MaintenanceAlertContext context,
            bool hasElementImage, bool hasMachinePositionImage)
        {
            var element = context.FailedElement;
            double usedPercent = LifeUsedPercent(context);

            AppendSectionTitle(sb, "FAILED ELEMENT", Accent);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");

            sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border:1px solid {Border};border-radius:10px;border-collapse:separate;overflow:hidden;\">");
            AppendKeyValue(sb, "Element", $"{Escape(element.TypeName)} #{element.OrderOfElementAtMachine}", true);
            AppendKeyValue(sb, "Element Type", Escape(element.TypeName), false);
            AppendKeyValue(sb, "Identification / Order Number", $"#{element.OrderOfElementAtMachine}", true);
            AppendKeyValue(sb, "Machine Position", Escape(element.Description), false);
            AppendKeyValue(sb, "Default Life", $"{Number(element.DefaultLife)} {Escape(element.UnitLabel)}", true);
            AppendKeyValue(sb, "Consumed Life at Failure", $"<span style=\"color:{DangerRed};font-weight:bold;\">{Number(context.ConsumedLifeAtFailure)} {Escape(element.UnitLabel)}</span>", false);
            AppendKeyValue(sb, "Remaining Life", $"{Number(Math.Max(0, element.DefaultLife - context.ConsumedLifeAtFailure))} {Escape(element.UnitLabel)}", true);
            AppendKeyValue(sb, "Life Used", $"<strong>{Percent(usedPercent)}</strong>", false);
            AppendKeyValue(sb, "Element Price", Money(element.Price), true);
            sb.Append("</table>");

            sb.Append(BuildProgressBar(usedPercent, DangerRed));
            sb.Append("</td></tr>");

            if (hasMachinePositionImage)
            {
                sb.Append("<tr><td style=\"padding:14px 30px 0 30px;\">");
                sb.Append($"<div style=\"font-size:11px;color:{SubText};text-transform:uppercase;letter-spacing:1px;margin-bottom:8px;\">Location of this element inside the machine</div>");
                sb.Append($"<img src=\"cid:{MachinePositionImageContentId}\" alt=\"Element position in the machine\" style=\"max-width:100%;border-radius:10px;border:1px solid {Border};\">");
                sb.Append("</td></tr>");
            }

            if (hasElementImage)
            {
                sb.Append("<tr><td style=\"padding:14px 30px 0 30px;\">");
                sb.Append($"<div style=\"font-size:11px;color:{SubText};text-transform:uppercase;letter-spacing:1px;margin-bottom:8px;\">Component reference picture</div>");
                sb.Append($"<img src=\"cid:{ElementImageContentId}\" alt=\"{Escape(element.TypeName)}\" style=\"max-width:260px;border-radius:10px;border:1px solid {Border};background:#FFFFFF;\">");
                sb.Append("</td></tr>");
            }
        }

        // --------------------------------------------------------- machine state

        private static void AppendMachineState(StringBuilder sb, MaintenanceAlertContext context)
        {
            var state = context.MachineState;

            AppendSectionTitle(sb, "MACHINE STATUS AT FAILURE", PrimaryDark);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");
            sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border:1px solid {Border};border-radius:10px;border-collapse:separate;overflow:hidden;\">");
            AppendKeyValue(sb, "Machine State", $"<span style=\"color:{DangerRed};font-weight:bold;\">{Escape(state.MachineStatus)}</span>", true);
            AppendKeyValue(sb, "Production / Cutting State", Escape(state.ProductionState), false);
            AppendKeyValue(sb, "Machine Runtime", Escape(state.MachineRuntime), true);
            AppendKeyValue(sb, "Production Quantity Since Machine Start", $"{state.ProductionQuantityInCubicMeter.ToString("F4", Culture)} m&sup3;", false);
            AppendKeyValue(sb, "Electricity Consumed Since Machine Start", $"{state.ConsumedElectricity.ToString("F4", Culture)} kWh", true);
            AppendKeyValue(sb, "Current Machine Speed", $"{state.MachineSpeedInRPM.ToString("F1", Culture)} RPM", false);
            AppendKeyValue(sb, "Wood Being Processed", Escape(state.WoodType), true);
            sb.Append("</table></td></tr>");
        }

        // ---------------------------------------------- failed element history

        private static void AppendRecentMaintenance(StringBuilder sb, MaintenanceAlertContext context)
        {
            var records = context.FailedElementRecentMaintenance;
            var element = context.FailedElement;

            AppendSectionTitle(sb, $"RECENT MAINTENANCE OF FAILED ELEMENT (LAST {context.MaintenanceHistoryWindowInDays} DAYS)", Gold);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");

            sb.Append($"<p style=\"margin:0 0 10px 0;font-size:13px;color:{SubText};\">Number of maintenance operations in the last {context.MaintenanceHistoryWindowInDays} days: " +
                      $"<strong style=\"color:{DarkText};font-size:15px;\">{records.Count}</strong></p>");

            if (records.Count == 0)
            {
                AppendEmptyNotice(sb, $"No maintenance was performed on {Escape(element.TypeName)} #{element.OrderOfElementAtMachine} during the last {context.MaintenanceHistoryWindowInDays} days.");
            }
            else
            {
                sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;font-size:12.5px;border:1px solid {Border};border-radius:10px;overflow:hidden;\">");
                AppendTableHeader(sb, Gold, "Maintenance Date", "Done By", "Cost", "Element Price", "Stopping Time Cost");
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

        // ------------------------------------------------- predictive section

        private static void AppendApproachingFailure(StringBuilder sb, MaintenanceAlertContext context)
        {
            var elements = context.ElementsApproachingFailure;

            AppendSectionTitle(sb, "OTHER ELEMENTS APPROACHING FAILURE", DangerRed);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");
            sb.Append($"<p style=\"margin:0 0 10px 0;font-size:12.5px;color:{SubText};\">Elements whose consumed life has reached {Percent(context.WarningThresholdPercent)} or more of their expected life.</p>");

            if (elements.Count == 0)
            {
                AppendEmptyNotice(sb, $"No other element has passed the {Percent(context.WarningThresholdPercent)} warning threshold.");
            }
            else
            {
                sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;font-size:12.5px;border:1px solid {Border};border-radius:10px;overflow:hidden;\">");
                AppendTableHeader(sb, DangerRed, "Element", "Position", "Consumed Life", "Default Life", "Remaining Life", "Life Used");

                int index = 0;
                foreach (var element in elements)
                {
                    double percent = element.LifeUsedPercentage;
                    string colour = percent >= MaintenanceSettings.CriticalThresholdPercent ? DangerRed : Gold;
                    AppendTableRow(sb, index++ % 2 == 1,
                        $"{Escape(element.TypeName)} #{element.OrderOfElementAtMachine}",
                        Escape(ShortPosition(element.Description)),
                        $"{Number(element.TotalConsumedLife)}",
                        $"{Number(element.DefaultLife)}",
                        $"{Number(element.RemainingLife)}",
                        $"<span style=\"color:{colour};font-weight:bold;\">{Percent(percent)}</span>");
                }
                sb.Append("</table>");
            }

            sb.Append("</td></tr>");
        }

        // ------------------------------------------- last general maintenance

        private static void AppendLastGeneralMaintenance(StringBuilder sb, MaintenanceAlertContext context)
        {
            AppendSectionTitle(sb, "LAST GENERAL MACHINE MAINTENANCE", DeepTeal);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");

            var last = context.LastMachineMaintenance;
            if (last == null)
            {
                AppendEmptyNotice(sb, "No previous maintenance record is available.");
            }
            else
            {
                string maintained = last.Element?.ElementInformation != null
                    ? $"{Escape(last.Element.ElementInformation.Name)} #{last.Element.OrderOfElementAtMachine}"
                    : "N/A";

                sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border:1px solid {Border};border-radius:10px;border-collapse:separate;overflow:hidden;\">");
                AppendKeyValue(sb, "Last Maintenance Date", Escape(last.MaintenanceDate.ToString("yyyy-MM-dd HH:mm", Culture)), true);
                AppendKeyValue(sb, "Element(s) Maintained", maintained, false);
                AppendKeyValue(sb, "Done By", Escape(last.DoneBy), true);
                AppendKeyValue(sb, "Maintenance Cost", Money(last.Cost), false);
                AppendKeyValue(sb, "Element Price", Money(last.ElementPrice), true);
                AppendKeyValue(sb, "Stopping Time Cost", Money(last.StoppingTimeCost), false);
                sb.Append("</table>");
            }

            sb.Append("</td></tr>");
        }

        private static void AppendAttachmentsNote(StringBuilder sb, MaintenanceAlertContext context, bool hasElementImage)
        {
            var element = context.FailedElement;
            bool hasCatalog = !string.IsNullOrWhiteSpace(element.CatalogFileName);

            AppendSectionTitle(sb, "ATTACHMENTS", SubText);
            sb.Append("<tr><td style=\"padding:0 30px 4px 30px;\">");
            sb.Append($"<ol style=\"margin:0;padding-left:20px;font-size:13px;color:{SubText};line-height:1.8;\">");
            sb.Append(hasElementImage
                ? "<li>Failed element picture</li>"
                : $"<li style=\"color:{SubText};\">Failed element picture &mdash; <em>not available</em></li>");
            sb.Append(hasCatalog
                ? $"<li>Element catalogue &mdash; {Escape(element.CatalogFileName!)}</li>"
                : "<li><em>Element catalogue not available</em></li>");
            sb.Append("</ol></td></tr>");
        }

        private static void AppendFooter(StringBuilder sb)
        {
            sb.Append("<tr><td style=\"padding:22px 30px 8px 30px;\">");
            sb.Append($"<div style=\"background:#FEF2F2;border-left:4px solid {DangerRed};border-radius:0 8px 8px 0;padding:14px 16px;font-size:13px;line-height:1.6;color:{DarkText};\">" +
                      "The machine has been stopped automatically to allow the maintenance department to inspect the affected component. " +
                      "Production cannot resume safely until the element is inspected or replaced.</div>");
            sb.Append("</td></tr>");

            sb.Append($"<tr><td style=\"padding:20px 30px 26px 30px;border-top:1px solid {Border};margin-top:10px;\">");
            sb.Append($"<div style=\"font-size:13px;color:{DarkText};\">Regards,</div>");
            sb.Append($"<div style=\"font-size:13px;font-weight:bold;color:{PrimaryDark};margin-top:3px;\">Production Line Monitoring System</div>");
            sb.Append($"<div style=\"font-size:11px;color:{SubText};margin-top:10px;\">This message was generated automatically by the MR200 predictive maintenance system. Please do not reply.</div>");
            sb.Append("</td></tr>");
        }

        // ------------------------------------------------------------- helpers

        private static void AppendSectionTitle(StringBuilder sb, string title, string colour)
        {
            sb.Append("<tr><td style=\"padding:24px 30px 12px 30px;\">");
            sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>");
            sb.Append($"<td style=\"width:4px;background:{colour};border-radius:2px;\">&nbsp;</td>");
            sb.Append($"<td style=\"padding-left:10px;font-size:12.5px;font-weight:bold;letter-spacing:1.1px;color:{colour};\">{Escape(title)}</td>");
            sb.Append("</tr></table></td></tr>");
        }

        /// <summary>
        /// Emits one label/value row. The value is expected to be caller-escaped or
        /// deliberate markup, so it is written through as-is.
        /// </summary>
        private static void AppendKeyValue(StringBuilder sb, string key, string valueHtml, bool shaded)
        {
            string background = shaded ? "#FAFBFC" : "#FFFFFF";
            sb.Append($"<tr style=\"background:{background};\">");
            sb.Append($"<td style=\"padding:9px 14px;font-size:12.5px;color:{SubText};width:46%;border-bottom:1px solid {Border};\">{Escape(key)}</td>");
            sb.Append($"<td style=\"padding:9px 14px;font-size:12.5px;color:{DarkText};font-weight:600;border-bottom:1px solid {Border};\">{valueHtml}</td>");
            sb.Append("</tr>");
        }

        private static void AppendTableHeader(StringBuilder sb, string colour, params string[] headers)
        {
            sb.Append($"<tr style=\"background:{colour};\">");
            foreach (var header in headers)
                sb.Append($"<th align=\"left\" style=\"padding:9px 12px;color:#FFFFFF;font-size:11px;letter-spacing:0.6px;text-transform:uppercase;font-weight:600;\">{Escape(header)}</th>");
            sb.Append("</tr>");
        }

        private static void AppendTableRow(StringBuilder sb, bool shaded, params string[] cells)
        {
            string background = shaded ? "#FAFBFC" : "#FFFFFF";
            sb.Append($"<tr style=\"background:{background};\">");
            foreach (var cell in cells)
                sb.Append($"<td style=\"padding:9px 12px;border-top:1px solid {Border};color:{DarkText};\">{cell}</td>");
            sb.Append("</tr>");
        }

        private static void AppendEmptyNotice(StringBuilder sb, string message)
        {
            sb.Append($"<div style=\"background:{LightBg};border:1px dashed {Border};border-radius:10px;padding:16px;font-size:13px;color:{SubText};text-align:center;\">{message}</div>");
        }

        private static string BuildProgressBar(double percent, string colour)
        {
            double clamped = Math.Max(0, Math.Min(100, percent));
            var sb = new StringBuilder();
            sb.Append($"<div style=\"margin-top:12px;background:{LightBg};border-radius:20px;height:12px;overflow:hidden;\">");
            sb.Append($"<div style=\"width:{clamped.ToString("F1", Culture)}%;background:{colour};height:12px;border-radius:20px;\"></div>");
            sb.Append("</div>");
            sb.Append($"<div style=\"font-size:11px;color:{SubText};margin-top:5px;\">{Percent(percent)} of expected life consumed</div>");
            return sb.ToString();
        }

        private static double LifeUsedPercent(MaintenanceAlertContext context)
        {
            double defaultLife = context.FailedElement.DefaultLife;
            return defaultLife <= 0 ? 0 : context.ConsumedLifeAtFailure / defaultLife * 100.0;
        }

        /// <summary>Trims the long stored description down to something a table cell can hold.</summary>
        private static string ShortPosition(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return "N/A";
            int separator = description.IndexOf(" - ", StringComparison.Ordinal);
            return separator > 0 ? description.Substring(0, separator) : description;
        }

        private static string Number(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "N/A" : value.ToString("N0", Culture);

        private static string Percent(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "N/A" : value.ToString("F2", Culture) + "%";

        private static string Money(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "N/A" : "$" + value.ToString("N2", Culture);

        private static string Escape(string? value) =>
            string.IsNullOrEmpty(value) ? "" : System.Net.WebUtility.HtmlEncode(value);
    }
}
