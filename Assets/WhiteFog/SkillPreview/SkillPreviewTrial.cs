using SoftKitty;
using UnityEngine;

// Shared baseline for the dedicated workbench scene and its HTML5 player.
public sealed class SkillPreviewTrial : MonoBehaviour
{
    private float previousMaximumDelta;

    public static void Begin(EntityComponent hero)
    {
        hero.gameObject.AddComponent<SkillPreviewTrial>();
        CombatRandom.BeginBattle(CombatRandom.Seed);
        GameManager.EntityManagerData.RuntimeInit();
        GameManager.SetPlayerInstance(hero);
    }

    private void Awake()
    {
        previousMaximumDelta = Time.maximumDeltaTime;
        // A slow render frame must not skip over successive releases or their delayed hit checks.
        Time.maximumDeltaTime = Mathf.Min(previousMaximumDelta, .02f);
    }

    private void OnDestroy() => Time.maximumDeltaTime = previousMaximumDelta;
}
