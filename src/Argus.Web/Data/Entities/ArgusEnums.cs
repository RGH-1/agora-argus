namespace Argus.Web.Data.Entities;

public enum IncidentStatus { Open, Acknowledged, Resolved }

public enum MetricAggregation { Count, Sum, Average, Min, Max, Latest }
public enum TimeBucket { Minute, Hour, Day, Week }
public enum MetricGroupBy { None, Dimension, Target }
public enum ChartType { Line, Bar, NumberTile }