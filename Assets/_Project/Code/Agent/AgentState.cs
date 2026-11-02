namespace Forge.Agent
{
    public enum AgentState
    {
        /// <summary>Nothing in progress; a message can be sent.</summary>
        Idle,

        /// <summary>The agent is working on a message (and building into the draft).</summary>
        Working,

        /// <summary>The agent is done and its build waits for Accept, Retry or Discard; a follow-up message refines it.</summary>
        Reviewing,
    }
}
