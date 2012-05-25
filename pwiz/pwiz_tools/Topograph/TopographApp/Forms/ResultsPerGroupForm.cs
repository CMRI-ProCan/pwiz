﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ResultsPerGroupForm : WorkspaceForm
    {
        private TopographViewContext _viewContext;
        public ResultsPerGroupForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            foreach (var evviesFilter in Enum.GetValues(typeof(EvviesFilterEnum)))
            {
                comboEvviesFilter.Items.Add(evviesFilter);
            }
            comboEvviesFilter.SelectedIndex = 0;
            _viewContext = new TopographViewContext(Workspace, typeof(DisplayRow), new[]{GetDefaultViewSpec(cbxByProtein.Checked)});
            navBar1.ViewContext = _viewContext;
            tbxMinAuc.Text = Workspace.GetAcceptMinAreaUnderChromatogramCurve().ToString();
            tbxMinScore.Text = Workspace.GetAcceptMinDeconvolutionScore().ToString();
        }

        public EvviesFilterEnum EvviesFilter
        {
            get { return (EvviesFilterEnum) comboEvviesFilter.SelectedIndex; }
            set { comboEvviesFilter.SelectedIndex = (int) value; }
        }

        public struct GroupKey
        {
            public GroupKey(String cohort, double? timePoint, string sample, string file) : this()
            {
                Cohort = cohort;
                TimePoint = timePoint;
                Sample = sample;
                File = file;
            }
            public String Cohort { get; private set; }
            public double? TimePoint { get; private set; }
            public string Sample { get; private set; }
            public string File { get; private set; }
            public override string ToString()
            {
                if (File != null)
                {
                    return File;
                }
                var result = new StringBuilder();
                if (!string.IsNullOrEmpty(Cohort))
                {
                    result.Append(Cohort);
                }
                if (TimePoint.HasValue)
                {
                    if (result.Length > 0)
                    {
                        result.Append(" ");
                    }
                    result.Append(TimePoint);
                }
                if (!string.IsNullOrEmpty(Sample))
                {
                    if (result.Length > 0)
                    {
                        result.Append(" ");
                    }
                    result.Append(Sample);
                }
                return result.ToString();
            }
        }

        public class ResultData
        {
            public ResultData(IList<HalfLifeCalculator.RowData> rowDatas)
            {
                TracerPercentByArea = ToStatistics(rowDatas.Select(rd=>rd.TracerPercent));
                IndTurnover = ToStatistics(rowDatas.Select(rd=>rd.IndTurnover));
                IndPrecursorEnrichment = ToStatistics(rowDatas.Select(rd => rd.IndPrecursorEnrichment));
                AvgTurnover = ToStatistics(rowDatas.Select(rd => rd.AvgTurnover));
                AvgPrecursorEnrichment = ToStatistics(rowDatas.Select(rd => rd.AvgPrecursorEnrichment));
                AreaUnderCurve = ToStatistics(rowDatas.Select(rd => rd.AreaUnderCurve));
            }
            public Statistics TracerPercentByArea { get; private set; }
            public Statistics IndTurnover { get; private set; }
            public Statistics IndPrecursorEnrichment { get; private set; }
            public Statistics AvgTurnover { get; private set; }
            public Statistics AvgPrecursorEnrichment { get; private set; }
            public Statistics AreaUnderCurve { get; private set; }

        }

        public class DisplayRow
        {
            private readonly HalfLifeCalculator.ResultRow _resultRow;
            private readonly HalfLifeCalculator _halfLifeCalculator;
            internal DisplayRow(HalfLifeCalculator halfLifeCalculator, HalfLifeCalculator.ResultRow resultRow)
            {
                _resultRow = resultRow;
                _halfLifeCalculator = halfLifeCalculator;
                Results = new Dictionary<GroupKey, GroupResult>();
            }

            public Peptide Peptide { get { return _resultRow.Peptide; }}
            public string ProteinName { get { return _resultRow.ProteinName; } }
            public string ProteinDescription { get { return _resultRow.ProteinDescription; } }
            public string ProteinKey { get { return Workspace.GetProteinKey(ProteinName, ProteinDescription); } }
            Workspace Workspace { get { return _halfLifeCalculator.Workspace; } }
            [OneToMany(IndexDisplayName = "Group", ItemDisplayName= "Result")]
            public IDictionary<GroupKey, GroupResult> Results { get; private set; }
            public HalfLifeCalculator GetHalfLifeCalculator()
            {
                return _halfLifeCalculator;
            }

        }

        public class GroupResult
        {
            private ResultsPerGroupForm _form;
            private DisplayRow _displayRow;
            private GroupKey _groupKey;
            private ResultData _resultData;
            internal GroupResult(ResultsPerGroupForm form, DisplayRow displayRow, GroupKey groupKey, ResultData resultData)
            {
                _form = form;
                _displayRow = displayRow;
                _groupKey = groupKey;
                _resultData = resultData;
            }

            public string Cohort { get { return _groupKey.Cohort; } }
            public double? TimePoint { get { return _groupKey.TimePoint; } }
            public string Sample { get { return _groupKey.Sample; } }
            public LinkValue<Result> TracerPercent
            {
                get
                {
                    return new LinkValue<Result>(Result.FromStatistics(_resultData.TracerPercentByArea), 
                        GetClickEventHandler(HalfLifeCalculationType.TracerPercent));
                }
            }
            public LinkValue<Result> IndTurnover
            {
                get
                {
                    return new LinkValue<Result>(Result.FromStatistics(_resultData.IndTurnover), 
                        GetClickEventHandler(HalfLifeCalculationType.IndividualPrecursorPool));
                }
            }
            public Result IndPrecursorEnrichment { get { return Result.FromStatistics(_resultData.IndPrecursorEnrichment); } }
            public LinkValue<Result> AvgTurnover
            {
                get
                {
                    return new LinkValue<Result>
                        (
                        Result.FromStatistics(_resultData.AvgTurnover), GetClickEventHandler(HalfLifeCalculationType.GroupPrecursorPool));
                }
            }
            public Result AvgPrecursorEnrichment { get { return Result.FromStatistics(_resultData.AvgPrecursorEnrichment); } }
            public Result AreaUnderCurve { get { return Result.FromStatistics(_resultData.AreaUnderCurve); } }
            public ResultData GetResultData()
            {
                return _resultData;
            }
            private EventHandler GetClickEventHandler(HalfLifeCalculationType halfLifeCalculationType)
            {
                return (sender, args) =>
                           {
                               var cohortBuilder = new StringBuilder();
                               if (!string.IsNullOrEmpty(Cohort))
                               {
                                   cohortBuilder.Append(Cohort);
                               }
                               if (!string.IsNullOrEmpty(Sample))
                               {
                                   if (cohortBuilder.Length > 0)
                                   {
                                       cohortBuilder.Append(" ");
                                   }
                                   cohortBuilder.Append(Sample);
                               }
                               var halfLifeForm = new HalfLifeForm(_form.Workspace)
                                       {
                                           Peptide = _displayRow.Peptide == null ? "" : _displayRow.Peptide.Sequence,
                                           ProteinName = _displayRow.ProteinName,
                                           BySample = _displayRow.GetHalfLifeCalculator().BySample,
                                           Cohort = cohortBuilder.ToString(),
                                           MinScore = double.Parse(_form.tbxMinScore.Text),
                                           MinAuc = double.Parse(_form.tbxMinAuc.Text),
                                           HalfLifeCalculationType = halfLifeCalculationType,
                                           EvviesFilter = _displayRow.GetHalfLifeCalculator().EvviesFilter,
                                       };
                                halfLifeForm.Show(_form.DockPanel, _form.DockState);
                           };
            }
        }

        public class Result
        {
            private Statistics _statistics;
            Result(Statistics statistics)
            {
                _statistics = statistics;
            }

            public double Mean { get { return _statistics.Mean(); } }
            public double StdDev { get { return _statistics.StdDev(); } }
            public double StdErr { get { return _statistics.StdErr(); } }
            public double Min { get { return _statistics.Min(); } }
            public double Max { get { return _statistics.Max(); } }
            public int Count { get { return _statistics.Length; } }
            public override string ToString()
            {
                if (_statistics == null || _statistics.Length == 0)
                {
                    return "No data";
                }
                var stdDev = StdDev;
                if (double.IsNaN(stdDev))
                {
                    return Mean.ToString("0.###");
                }
                return Mean.ToString("0.###") + "+/-" + StdDev.ToString("0.###");
            }
            public static Result FromStatistics(Statistics statistics)
            {
                return new Result(statistics);
            }
        }

        private void btnRequery_Click(object sender, EventArgs e)
        {
            var halfLifeCalculator = new HalfLifeCalculator(Workspace, HalfLifeCalculationType.GroupPrecursorPool)
                                         {
                                             EvviesFilter = EvviesFilter,
                                             MinScore = double.Parse(tbxMinScore.Text),
                                             MinAuc =  double.Parse(tbxMinAuc.Text),
                                             ByProtein = cbxByProtein.Checked, 
                                             BySample = cbxGroupBySample.Checked,
                                             ByFile = cbxGroupByFile.Checked,
                                         };
            using (var longWaitDialog = new LongWaitDialog(TopLevelControl, "Calculating Half Lives"))
            {
                var longOperationBroker = new LongOperationBroker(halfLifeCalculator, longWaitDialog);
                if (!longOperationBroker.LaunchJob())
                {
                    return;
                }
            }
            bool byCohort = cbxGroupByCohort.Checked;
            bool byTimePoint = cbxGroupByTimePoint.Checked;
            bool bySample = cbxGroupBySample.Checked;
            bool byFile = cbxGroupByFile.Checked;
            var displayRows = new List<DisplayRow>();
            foreach (var resultRow in halfLifeCalculator.ResultRows)
            {
                var displayRow = new DisplayRow(halfLifeCalculator, resultRow);
                var rowDatasByCohort = new Dictionary<GroupKey, List<HalfLifeCalculator.RowData>>();
                foreach (var halfLife in resultRow.HalfLives)
                {
                    if (resultRow.HalfLives.Count > 1 && string.IsNullOrEmpty(halfLife.Key) != byCohort)
                    {
                        continue;
                    }
                    foreach (var rowData in halfLife.Value.FilteredRowDatas)
                    {
                        GroupKey cohortKey = new GroupKey(
                            byCohort ? rowData.MsDataFile.Cohort : null,
                            byTimePoint ? rowData.MsDataFile.TimePoint : null,
                            bySample ? rowData.MsDataFile.Sample : null,
                            byFile ? rowData.MsDataFile.Name : null);
                        List<HalfLifeCalculator.RowData> list;
                        if (!rowDatasByCohort.TryGetValue(cohortKey, out list))
                        {
                            list = new List<HalfLifeCalculator.RowData>();
                            rowDatasByCohort.Add(cohortKey, list);
                        }
                        list.Add(rowData);
                    }
                }
                foreach (var cohortRowDatas in rowDatasByCohort)
                {
                    displayRow.Results.Add(cohortRowDatas.Key, new GroupResult(this, displayRow, cohortRowDatas.Key, new ResultData(cohortRowDatas.Value)));
                }
                displayRows.Add(displayRow);
            }
            var viewInfo = dataGridView1.BindingListView.ViewInfo;
            if (viewInfo == null || "default" == viewInfo.Name)
            {
                dataGridView1.BindingListView.ViewInfo = new ViewInfo(_viewContext.ParentColumn, GetDefaultViewSpec(halfLifeCalculator.ByProtein));
            }
            dataGridView1.BindingListView.RowSource = displayRows;
            dataGridViewSummary.Rows.Clear();
            SetSummary("Tracer %", displayRows.Select(dr=>dr.Results).SelectMany(r=>r.Values
                .Select(cohortResult=>cohortResult.GetResultData().TracerPercentByArea)));
            SetSummary("Precursor Enrichment", displayRows.Select(dr=>dr.Results).SelectMany(r=>r.Values
                .Select(cohortResult=>cohortResult.GetResultData().IndPrecursorEnrichment)));
            SetSummary("Turnover", displayRows.Select(dr=>dr.Results).SelectMany(r=>r.Values
                .Select(cohortResult=>cohortResult.GetResultData().IndTurnover)));
            SetSummary("Area Under Curve", displayRows.Select(dr=>dr.Results).SelectMany(r=>r.Values
                .Select(cohortResult=>cohortResult.GetResultData().AreaUnderCurve)));
        }

        private ViewSpec GetDefaultViewSpec(bool byProtein)
        {
            List<ColumnSpec> columnSpecs = new List<ColumnSpec>();
            if (!byProtein)
            {
                columnSpecs.Add(new ColumnSpec().SetName("Peptide"));
            }
            columnSpecs.Add(new ColumnSpec().SetName("ProteinName"));
            columnSpecs.Add(new ColumnSpec().SetName("ProteinDescription"));
            columnSpecs.Add(new ColumnSpec().SetName("Results.[].Value.TracerPercent"));
            columnSpecs.Add(new ColumnSpec().SetName("Results.[].Value.AvgTurnover"));
            columnSpecs.Add(new ColumnSpec().SetName("Results.[].Value.AreaUnderCurve"));
            return new ViewSpec().SetName("default").SetColumns(columnSpecs);
        }

        private DataGridViewRow SetSummary(String quantity, IEnumerable<Statistics> lstStats)
        {
            var row = dataGridViewSummary.Rows[dataGridViewSummary.Rows.Add()];
            row.Cells[colSummaryQuantity.Index].Value = quantity;
            var lstValues = new List<double>();
            var lstStdErr = new List<double>();
            var lstStdDev = new List<double>();
            var totalCount = 0;
            foreach (var stats in lstStats)
            {
                totalCount += stats.Length;
                if (stats.Length > 0)
                {
                    lstValues.Add(stats.Mean());
                }
                if (stats.Length > 1)
                {
                    if (double.IsNaN(stats.StdDev()))
                    {
                        Console.Out.WriteLine(stats);
                    }
                    lstStdErr.Add(stats.StdErr());
                    lstStdDev.Add(stats.StdDev());
                }
            }
            row.Cells[colSummaryValueCount.Index].Value = totalCount;
            row.Cells[colSummaryStdDevStdErrCount.Index].Value = lstStdErr.Count;
            row.Cells[colSummaryAvgValue.Index].Value = new Statistics(lstValues.ToArray()).Mean();
            row.Cells[colSummaryMedianValue.Index].Value = new Statistics(lstValues.ToArray()).Median();
            row.Cells[colSummaryMeanStdDev.Index].Value = new Statistics(lstStdDev.ToArray()).Mean();
            row.Cells[colSummaryMeanStdErr.Index].Value = new Statistics(lstStdErr.ToArray()).Mean();
            return row;
        }

        private static Statistics ToStatistics(IEnumerable<double> values)
        {
            return ToStatistics(values.Cast<double?>());
        }
        private static Statistics ToStatistics(IEnumerable<double?> values)
        {
            return new Statistics(values.Where(v=>v.HasValue && !double.IsNaN(v.Value)).Cast<double>().ToArray());
        }

        private void cbxGroupByFile_CheckedChanged(object sender, EventArgs e)
        {
            cbxGroupByCohort.Enabled = cbxGroupByTimePoint.Enabled = cbxGroupBySample.Enabled = !cbxGroupByFile.Checked;
        }
    }
}