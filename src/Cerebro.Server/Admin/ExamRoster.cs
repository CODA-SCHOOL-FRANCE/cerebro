using System.Text.Json.Serialization;

namespace Cerebro.Server.Admin;

// Format du fichier fourni par l'admin pour provisionner une épreuve (export existant de l'école).
// L'id de chaque étudiant sert à la fois d'identifiant candidat et de secret de connexion :
// pas besoin de générer un jeton séparé, ce fichier n'est distribué qu'à l'admin/aux correcteurs.
public sealed record ExamRosterFile(
    string Ec,
    string Date,
    bool Rattrapage,
    Dictionary<string, ExamRosterStudent> Etudiants,
    List<ExamRosterCorrector>? Correcteurs,
    string? Diplome);

public sealed record ExamRosterStudent(
    string Nom,
    string Id,
    string? Promo,
    [property: JsonPropertyName("drive_folder_id")] string? DriveFolderId);

public sealed record ExamRosterCorrector(string Nom, string Email);
