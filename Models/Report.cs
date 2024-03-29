﻿using PlayOffsApi.Enum;

namespace PlayOffsApi.Models;

public class Report
{
    public int Id { get; set; }
    public Guid AuthorId { get; set; }
    public bool Completed { get; set; }
    public ReportType ReportType { get; set; }
    public Guid? ReportedUserId { get; set; }
    public Guid? ReportedPlayerTempId { get; set; }
    public int? ReportedTeamId { get; set; }
    public int? ReportedChampionshipId { get; set; }
    public string Description { get; set; }
    public TypeOfViolation Violation { get; set; }
    public string ReportedTeamName { get; set; }
    public string ReportedChampionsipName { get; set; }
    public string ReportedUserName { get; set; }
}