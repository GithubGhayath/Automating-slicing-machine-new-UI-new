using DataAccess.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MR200.UI.Database.Utility
{
    public static class PdfReportGenerator
    {
        public static void GenerateProcessReport(OperationsProcess process, string outputPath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var duration = process.auditTimestamp.EndAt - process.auditTimestamp.StartAt;
            double volume = process.ProductionCondition.ProductionVolume;
            double energy = process.OperationCondition.ConsumedElectricity;
            double productionRate = duration.TotalHours > 0 ? volume / duration.TotalHours : 0;
            double efficiency = energy > 0 ? volume / energy : 0;

            Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Background("#17463E").Padding(15).Column(col =>
                    {
                        col.Item().AlignCenter().Text("WOOD CUTTING PROCESS REPORT").FontColor(Colors.White).FontSize(16).Bold();
                        col.Item().AlignCenter().Text($"Process #{process.Id}").FontColor(Colors.White);
                    });

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Spacing(10);

                        void SectionHeader(string title) => column.Item().Background("#E05C1A").Padding(5).Text(title).FontColor(Colors.White).Bold();

                        SectionHeader("PROCESS INFORMATION");
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            table.Cell().Text("Process ID"); table.Cell().Text(process.Id.ToString());
                            table.Cell().Text("Start Time"); table.Cell().Text(process.auditTimestamp.StartAt.ToString("dd/MM/yyyy HH:mm:ss"));
                            table.Cell().Text("End Time"); table.Cell().Text(process.auditTimestamp.EndAt.ToString("dd/MM/yyyy HH:mm:ss"));
                            table.Cell().Text("Duration"); table.Cell().Text(duration.ToString(@"hh\:mm\:ss"));
                            table.Cell().Text("Wood Type"); table.Cell().Text(process.WoodType.Type);
                            table.Cell().Text("Category"); table.Cell().Text(process.WoodType.Category.ToString());
                        });

                        SectionHeader("PRODUCTION SUMMARY");
                        column.Item().Border(2).BorderColor("#E05C1A").Padding(10).Column(col =>
                        {
                            col.Item().Text($"Product Width : {process.ProductionCondition.ProductWidth} mm");
                            col.Item().Text($"Product Height : {process.ProductionCondition.ProductHeight} mm");
                            col.Item().Text($"Production Volume : {process.ProductionCondition.ProductionVolume:F3} m³");
                            col.Item().Text($"Total Cost : {process.ProductionCondition.TotalFees:F2}");
                        });

                        SectionHeader("MACHINE CONDITIONS");
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            table.Cell().Text("Cutting Speed"); table.Cell().Text($"{process.OperationCondition.CuttingSpeed:F2} m/min");
                            table.Cell().Text("Feed Rate"); table.Cell().Text($"{process.OperationCondition.FeedRate:F2} m/min");
                            table.Cell().Text("Shaft Speed"); table.Cell().Text($"{process.OperationCondition.SheftSpeed:F0} RPM");
                            table.Cell().Text("Consumed Electricity"); table.Cell().Text($"{process.OperationCondition.ConsumedElectricity:F2} kWh");
                        });

                        SectionHeader("CRITICAL CUTTING ANALYSIS");
                        var cv = process.CriticalValues;
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            void Add(string n, string v) { table.Cell().Text(n); table.Cell().Text(v); }
                            Add("Cutting Force", $"{cv.CuttingForce:F2} N");
                            Add("Active Force", $"{cv.ActiveForce:F2} N");
                            Add("Friction Force", $"{cv.FrictionForceOnRake:F2} N");
                            Add("Thrust Force", $"{cv.ThrustForce:F2} N");
                            Add("Shear Force", $"{cv.ShearForce:F2} N");
                            Add("Normal Force To Shear", $"{cv.NormalForceToShear:F2} N");
                            Add("Normal Force To Rake", $"{cv.NormalForceToRake:F2} N");
                            Add("Cutting Moment", $"{cv.CuttingMoment:F2} N.m");
                        });

                        SectionHeader("PERFORMANCE INDICATORS");
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Border(1).Padding(10).Column(c =>
                            {
                                c.Item().Text("Duration").Bold();
                                c.Item().Text(duration.ToString(@"hh\:mm\:ss"));
                            });
                            row.RelativeItem().Border(1).Padding(10).Column(c =>
                            {
                                c.Item().Text("Production Rate").Bold();
                                c.Item().Text($"{productionRate:F3} m³/hr");
                            });
                            row.RelativeItem().Border(1).Padding(10).Column(c =>
                            {
                                c.Item().Text("Energy Efficiency").Bold();
                                c.Item().Text($"{efficiency:F3} m³/kWh");
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Generated by MR200 - Page ");
                        text.CurrentPageNumber();
                    });
                });
            }).GeneratePdf(outputPath);
        }
    }
}
