using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public partial class ManiaActionInputTrigger : InputTrigger
    {
        public ManiaActionInputTrigger(string actionName)
            : base(actionName)
        {
        }
    }

    public partial class EzKeyCounterPro : Container
    {
        public readonly InputTrigger Trigger;
        public IBindable<int> CountPresses => Trigger.ActivationCount;

        public IBindable<bool> IsActive => isActive;

        private readonly Bindable<bool> isActive = new BindableBool();

        public EzKeyCounterPro(ManiaAction action)
        {
            // 将 ManiaAction 转换为 InputTrigger
            Trigger = new ManiaActionInputTrigger(action.ToString());

            Trigger.OnActivate += Activate;
            Trigger.OnDeactivate += Deactivate;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Trigger.IsActive)
                Activate();
        }

        protected virtual void Activate(bool forwardPlayback = true)
        {
            isActive.Value = true;
        }

        protected virtual void Deactivate(bool forwardPlayback = true)
        {
            isActive.Value = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            Trigger.OnActivate -= Activate;
            Trigger.OnDeactivate -= Deactivate;
        }
    }
}
