namespace Cerebro.Server.Admin;

// Format minimal du roster fourni pour provisionner une épreuve : uniquement ce qu'utilise
// ExamProvisioner (voir ExamProvisioner.cs). L'id de chaque étudiant sert à la fois d'identifiant
// candidat et de secret de connexion : pas besoin de générer un jeton séparé.
public sealed record ExamRosterFile(List<ExamRosterStudent> Etudiants);

public sealed record ExamRosterStudent(string Nom, string Id);
