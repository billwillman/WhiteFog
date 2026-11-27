using SoftKitty;
using System.Collections.Generic;
using UnityEngine;

// Shared baseline for the dedicated workbench scene and its HTML5 player.
public sealed class SkillPreviewTrial : MonoBehaviour
{
    private float previousMaximumDelta;

    public static void Begin(EntityComponent hero)
    {
        if (!hero.TryGetComponent<SkillPreviewTrial>(out _)) hero.gameObject.AddComponent<SkillPreviewTrial>();
        CombatRandom.BeginBattle(CombatRandom.Seed);
        GameManager.EntityManagerData.RuntimeInit();
        GameManager.SetPlayerInstance(hero);
    }

#if UNITY_EDITOR
    private readonly HashSet<GameObject> spawned = new HashSet<GameObject>();

    public static void BeginQuickPreview(EntityComponent hero, EntityComponent opponent)
    {
        var templates = GameManager.EntityManagerData.EntityData.EntityList;
        BeginQuickPreview(hero, opponent, templates.Find(t => t.uid == hero.uid), templates.Find(t => t.uid == opponent.uid));
    }

    public static void BeginQuickPreview(EntityComponent hero, EntityComponent opponent, Entity friendlyTemplate, Entity enemyTemplate)
    {
        if (friendlyTemplate == null || enemyTemplate == null)
            throw new System.InvalidOperationException("快速试放的双方均需绑定有效的 MCC 实体。");
        if (!hero.TryGetComponent<SkillPreviewTrial>(out var trial))
            trial = hero.gameObject.AddComponent<SkillPreviewTrial>();
        SkillPlayback.TemporaryObjectCreated -= trial.Track;
        foreach (var obj in trial.spawned)
        {
            if (!obj) continue;
            // Destroy runs at frame end. Disable now to stop releases and collisions before the next cast.
            obj.SetActive(false);
            Destroy(obj);
        }
        trial.spawned.Clear();
        SkillPlayback.TemporaryObjectCreated += trial.Track;
        CombatRandom.BeginBattle(CombatRandom.Seed);
        bool shared = friendlyTemplate == enemyTemplate;
        ResetActor(hero, friendlyTemplate, shared);
        ResetActor(opponent, enemyTemplate, shared);
        GameManager.Refresh();
        GameManager.SetPreviewPlayer(hero);
    }

    private void Track(GameObject obj)
    {
        if (obj.scene == gameObject.scene) spawned.Add(obj);
    }

    private static void ResetActor(EntityComponent actor, Entity template, bool independent)
    {
        actor.InterruptCast();
        actor.StopAllCoroutines();
        GameManager.EntityManagerData.ResetPreviewEntity(actor, template, independent);
        foreach (var animator in actor.GetComponentsInChildren<Animator>(true))
        {
            if (animator.TryGetComponent<SkillClipPlayer>(out var player)) player.StopClip();
            if (!animator.isActiveAndEnabled || !animator.runtimeAnimatorController) continue;
            animator.Rebind();
            animator.Update(0);
        }
    }
#endif

    private void Awake()
    {
        previousMaximumDelta = Time.maximumDeltaTime;
        // A slow render frame must not skip over successive releases or their delayed hit checks.
        Time.maximumDeltaTime = Mathf.Min(previousMaximumDelta, .02f);
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        SkillPlayback.TemporaryObjectCreated -= Track;
#endif
        Time.maximumDeltaTime = previousMaximumDelta;
    }
}
