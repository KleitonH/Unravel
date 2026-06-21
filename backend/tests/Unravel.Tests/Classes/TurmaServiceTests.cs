using Microsoft.EntityFrameworkCore;
using Unravel.Application.Classes.Ports;
using Unravel.Domain.Entities;
using Unravel.Domain.ValueObjects;
using Unravel.Infrastructure.Classes;
using Unravel.Infrastructure.Notifications;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Tests.Classes;

/// <summary>
/// Turmas — professor (Moderator) cria turma, convida alunos da plataforma,
/// aluno aceita/recusa; autorização por dono/convidado. EF InMemory.
/// </summary>
public class TurmaServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly TurmaService _sut;

    public TurmaServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _sut = new TurmaService(_db, new NotificationService(_db));
    }

    private User AddUser(string name, Role role)
    {
        var slug = name.Replace(" ", "").ToLower();
        var u = User.Create(name, Email.Create($"{slug}{Guid.NewGuid():N}@u.test"), "h");
        u.Role = role;
        _db.User.Add(u);
        _db.SaveChanges();
        return u;
    }

    private User AddTeacher(string name = "Prof") => AddUser(name, Role.Moderator);
    private User AddStudent(string name)          => AddUser(name, Role.Student);

    [Fact]
    public async Task Create_then_GetOwned()
    {
        var prof = AddTeacher();
        var dto = await _sut.CreateAsync(prof.Id, "Turma A", "desc", "🎓");

        Assert.True(dto.Id > 0);
        var owned = await _sut.GetOwnedAsync(prof.Id);
        Assert.Single(owned);
        Assert.Equal("Turma A", owned[0].Name);
        Assert.Equal(0, owned[0].MemberCount);
    }

    [Fact]
    public async Task Invite_creates_pending_and_notifies_student()
    {
        var prof = AddTeacher();
        var ana = AddStudent("Ana");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);

        var r = await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);
        Assert.Equal(TurmaActionOutcome.Ok, r.Outcome);

        // aluno vê o convite pendente + recebeu notificação
        var invites = await _sut.GetInvitesAsync(ana.Id);
        Assert.Single(invites);
        Assert.Equal("Turma A", invites[0].TurmaName);

        var notif = await _db.Notification.SingleAsync(n => n.UserId == ana.Id);
        Assert.Equal(NotificationType.ClassInvite, notif.Type);

        // ainda não conta como membro ativo
        var owned = await _sut.GetOwnedAsync(prof.Id);
        Assert.Equal(0, owned[0].MemberCount);
        Assert.Equal(1, owned[0].PendingCount);
    }

    [Fact]
    public async Task Invite_duplicate_rejected()
    {
        var prof = AddTeacher();
        var ana = AddStudent("Ana");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);

        await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);
        var again = await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);
        Assert.Equal(TurmaActionOutcome.AlreadyInvited, again.Outcome);
    }

    [Fact]
    public async Task Invite_nonStudent_rejected()
    {
        var prof = AddTeacher();
        var otherProf = AddTeacher("Prof2");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);

        var r = await _sut.InviteAsync(prof.Id, turma.Id, otherProf.Id);
        Assert.Equal(TurmaActionOutcome.NotAStudent, r.Outcome);
    }

    [Fact]
    public async Task Invite_nonOwner_rejected()
    {
        var prof = AddTeacher();
        var intruder = AddTeacher("Intruso");
        var ana = AddStudent("Ana");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);

        var r = await _sut.InviteAsync(intruder.Id, turma.Id, ana.Id);
        Assert.Equal(TurmaActionOutcome.NotAuthorized, r.Outcome);
    }

    [Fact]
    public async Task Accept_makes_active_and_appears_in_GetMine()
    {
        var prof = AddTeacher();
        var ana = AddStudent("Ana");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);
        var inv = await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);

        var r = await _sut.RespondInviteAsync(ana.Id, inv.Id!.Value, accept: true);
        Assert.Equal(TurmaActionOutcome.Ok, r.Outcome);

        var mine = await _sut.GetMineAsync(ana.Id);
        Assert.Single(mine);
        Assert.Equal("Turma A", mine[0].Name);

        var owned = await _sut.GetOwnedAsync(prof.Id);
        Assert.Equal(1, owned[0].MemberCount);
        Assert.Equal(0, owned[0].PendingCount);

        // convite some da lista de pendentes
        Assert.Empty(await _sut.GetInvitesAsync(ana.Id));
    }

    [Fact]
    public async Task Decline_removes_invite()
    {
        var prof = AddTeacher();
        var ana = AddStudent("Ana");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);
        var inv = await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);

        await _sut.RespondInviteAsync(ana.Id, inv.Id!.Value, accept: false);

        Assert.Empty(await _sut.GetInvitesAsync(ana.Id));
        Assert.Empty(await _sut.GetMineAsync(ana.Id));
        // pode reconvidar depois
        var again = await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);
        Assert.Equal(TurmaActionOutcome.Ok, again.Outcome);
    }

    [Fact]
    public async Task RespondInvite_byOther_rejected()
    {
        var prof = AddTeacher();
        var ana = AddStudent("Ana");
        var bia = AddStudent("Bia");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);
        var inv = await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);

        var r = await _sut.RespondInviteAsync(bia.Id, inv.Id!.Value, accept: true);
        Assert.Equal(TurmaActionOutcome.NotAuthorized, r.Outcome);
    }

    [Fact]
    public async Task RemoveMember_and_Leave()
    {
        var prof = AddTeacher();
        var ana = AddStudent("Ana");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);
        var inv = await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);
        await _sut.RespondInviteAsync(ana.Id, inv.Id!.Value, accept: true);

        // professor remove
        var rem = await _sut.RemoveMemberAsync(prof.Id, turma.Id, ana.Id);
        Assert.Equal(TurmaActionOutcome.Ok, rem.Outcome);
        Assert.Empty(await _sut.GetMineAsync(ana.Id));

        // reconvida + aceita, depois aluno sai sozinho
        var inv2 = await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);
        await _sut.RespondInviteAsync(ana.Id, inv2.Id!.Value, accept: true);
        var left = await _sut.LeaveAsync(ana.Id, turma.Id);
        Assert.Equal(TurmaActionOutcome.Ok, left.Outcome);
        Assert.Empty(await _sut.GetMineAsync(ana.Id));
    }

    [Fact]
    public async Task GetDetail_byNonOwner_returns_null()
    {
        var prof = AddTeacher();
        var intruder = AddTeacher("Intruso");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);

        Assert.NotNull(await _sut.GetDetailAsync(prof.Id, turma.Id));
        Assert.Null(await _sut.GetDetailAsync(intruder.Id, turma.Id));
    }

    [Fact]
    public async Task SearchStudents_annotates_relation()
    {
        var prof = AddTeacher();
        var ana = AddStudent("Ana Souza");
        AddStudent("Ana Lima");
        var turma = await _sut.CreateAsync(prof.Id, "Turma A", null, null);
        await _sut.InviteAsync(prof.Id, turma.Id, ana.Id);

        var results = await _sut.SearchStudentsAsync(prof.Id, turma.Id, "ana", 20);
        Assert.Equal(2, results.Count);
        Assert.Equal("invited", results.First(r => r.UserId == ana.Id).Relation);
        Assert.Contains(results, r => r.Relation == "none");
    }

    public void Dispose() => _db.Dispose();
}
