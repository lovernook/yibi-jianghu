using GameFramework.Procedure;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Yibi.App
{
    /// <summary>
    /// Actual Game Framework procedures. They coordinate lifetime and navigation;
    /// they do not contain battle rules or generate scene/UI objects.
    /// </summary>
    internal abstract class AppProcedure : ProcedureBase
    {
        protected readonly GameRoot Root;

        protected AppProcedure(GameRoot root) { Root = root; }

        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            Root.SetProcedure(GetType().Name);
        }

        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            base.OnUpdate(owner, elapsed, realElapsed);
            var desired = Root.DesiredProcedure();
            if (desired != GetType()) ChangeState(owner, desired);
        }
    }

    internal sealed class ProcedureBoot : AppProcedure
    {
        public ProcedureBoot(GameRoot root) : base(root) { }
    }

    internal sealed class ProcedureLoading : AppProcedure
    {
        public ProcedureLoading(GameRoot root) : base(root) { }
        protected override void OnEnter(ProcedureOwner owner)
        {
            base.OnEnter(owner);
            Root.BeginPendingLoad();
        }
        protected override void OnUpdate(ProcedureOwner owner, float elapsed, float realElapsed)
        {
            // A new request may arrive in the same frame as sceneLoaded, before
            // this procedure has left. It still needs to start exactly once.
            Root.BeginPendingLoad();
            base.OnUpdate(owner, elapsed, realElapsed);
        }
    }

    internal sealed class ProcedureError : AppProcedure
    {
        public ProcedureError(GameRoot root) : base(root) { }
    }
    internal sealed class ProcedureMainMenu : AppProcedure
    {
        public ProcedureMainMenu(GameRoot root) : base(root) { }
    }
    internal sealed class ProcedureWorld : AppProcedure
    {
        public ProcedureWorld(GameRoot root) : base(root) { }
    }
    internal sealed class ProcedurePractice : AppProcedure
    {
        public ProcedurePractice(GameRoot root) : base(root) { }
    }
    internal sealed class ProcedureLobby : AppProcedure
    {
        public ProcedureLobby(GameRoot root) : base(root) { }
    }
    internal sealed class ProcedureBattle : AppProcedure
    {
        public ProcedureBattle(GameRoot root) : base(root) { }
    }
    internal sealed class ProcedureWorkshop : AppProcedure
    {
        public ProcedureWorkshop(GameRoot root) : base(root) { }
    }
    internal sealed class ProcedureExternalScene : AppProcedure
    {
        public ProcedureExternalScene(GameRoot root) : base(root) { }
    }
}
