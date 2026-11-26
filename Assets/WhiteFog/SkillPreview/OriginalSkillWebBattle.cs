using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using SoftKitty;
using UnityEngine;
using UnityEngine.Scripting;

public sealed class OriginalSkillWebBattle : MonoBehaviour
{
    public OriginalSkillGraph[] skills;
    public string[] editorSkillIds;
    public GraphObject[] editorSkills;
    public EntityComponent hero, enemy;
    public Transform bird;
    private GameObject boss;
    private SpriteRenderer birdSprite;
    private readonly Vector3 center = new Vector3(0, 10, 20);
    private float elapsed;
    private bool ferry;
    private EntityComponent previewCaster, previewTarget;
    private float nextReadout;

    [System.Serializable]
    private sealed class EditorRequest
    {
        public string guid;
        public int level = 1;
        public float distance = 8, fallbackDamage = 10;
        public bool opponent;
    }

    [System.Serializable]
    private sealed class Readout
    {
        public string kind, text, casterStates, targetStates;
        public float casterHp, targetHp, loss, shield;
        public int segment;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void SkillEditorReadout(string json);
#endif
    private void Report(string kind, string text = "", int segment = 0, float loss = 0, float shield = 0)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SkillEditorReadout(JsonUtility.ToJson(new Readout {
            kind = kind, text = text, segment = segment, loss = loss, shield = shield,
            casterHp = previewCaster ? previewCaster.GetAttributeFloat("chp") : 0,
            targetHp = previewTarget ? previewTarget.GetAttributeFloat("chp") : 0,
            casterStates = States(previewCaster), targetStates = States(previewTarget)
        }));
#endif
    }

    private static string States(EntityComponent actor) => actor && actor.mData != null
        ? string.Join("、", actor.mData.GetOverTimeEffectList().Select(e => e.uid + " ×" + e.layer)) : "";

    [Preserve]
    public void PlayEditorSkill(string json)
    {
        var request = JsonUtility.FromJson<EditorRequest>(json);
        int index = System.Array.IndexOf(editorSkillIds, request.guid);
        if (index < 0 || index >= editorSkills.Length) { Report("error", "该技能尚未打入 WebGL 包"); return; }
        Time.timeScale = 1;
        StartCoroutine(Preview(editorSkills[index], request));
    }

    private IEnumerator Preview(GraphObject skill, EditorRequest request)
    {
        while (hero.mData == null || enemy.mData == null) yield return null;
        float distance = Mathf.Clamp(request.distance, 2, 20);
        hero.SetPosition(center + Vector3.back * distance * .5f, false);
        enemy.SetPosition(center + Vector3.forward * distance * .5f, false);
        hero.transform.forward = Vector3.forward;
        enemy.transform.forward = Vector3.back;
        if (bird) bird.rotation = Quaternion.LookRotation(-enemy.transform.forward);
        var original = skill as OriginalSkillGraph;
        SelectOpponent(original ? original.previewEnemy : null);
        Camera.main.orthographicSize = Mathf.Max(original && original.previewEnemy ? 5.9f : 3.75f, distance * .32f + 2.3f);
        hero.UpdateData();
        enemy.UpdateData();
        Physics.SyncTransforms();
        bool opponent = original ? original.previewAsOpponent : request.opponent;
        previewCaster = opponent ? enemy : hero;
        previewTarget = opponent ? hero : enemy;
        var go = new GameObject(skill.name);
        go.transform.position = previewCaster.transform.position;
        var damage = go.AddComponent<CombatDamage>();
        damage.mGraph = skill;
        damage.Init();
        if (!damage.inited) { Destroy(go); Report("error", "技能图初始化失败"); yield break; }
        damage.SetDynamicValue("skill.level", Mathf.Max(1, request.level));
        SkillDamageNumbers.Observe(previewCaster);
        SkillDamageNumbers.Observe(previewTarget);
        var host = SkillPlayback.On(damage);
        host.RetireAfter(1 + skill.RootSet.Actions.Select(a => a.WaitTime +
            (a.Type == ActionType.Multiple_Time ? Mathf.Max(0, a.Count-1) * a.Interval :
            a.Type == ActionType.For_X_Seconds ? a.KeepTime : 0)).DefaultIfEmpty(0).Max());
        host.OriginalProjectileImpact = (source, victim) => SkillPlayback.ApplyIncomingDamage(source, victim, Mathf.Max(0, request.fallbackDamage));
        Report("cast", skill.name);
        damage.DealDamageObserved(previewCaster.mData, previewTarget.mData,
            (index, victim, loss, shield) => Report("hit", victim.CombatRandomId, index, loss, shield));
    }

    [Preserve]
    public void InterruptEditorSkill(string unused)
    {
        if (previewCaster) { previewCaster.InterruptCast(); Report("interrupt"); }
    }

    [Preserve]
    public void Begin(string encounter)
    {
        CombatRandom.BeginBattle(CombatRandom.Seed);
        Time.timeScale = 1;
        StopAllCoroutines();
        ferry = encounter == "ferry";
        elapsed = 0;
        StartCoroutine(Prepare());
    }

    private IEnumerator Prepare()
    {
        while (hero.mData == null || enemy.mData == null) yield return null;
        hero.SetAttributeValue("chp", 50f);
        enemy.SetAttributeValue("chp", 50f);
        hero.SetPosition(center + Vector3.back * 4, false);
        hero.transform.forward = Vector3.forward;
        enemy.SetPosition(center + Vector3.forward * 4, false);
        enemy.transform.forward = Vector3.back;
        bird.rotation = Quaternion.LookRotation(-enemy.transform.forward);
        SelectOpponent(null);
        hero.UpdateData();
        enemy.UpdateData();
        SkillDamageNumbers.Observe(hero);
        SkillDamageNumbers.Observe(enemy);
    }

    // The H5 impostor converter supplies its front tile as bottom-up RGBA32 pixels.
    [Preserve]
    public void SetBirdSprite(string encoded)
    {
        if (birdSprite || string.IsNullOrEmpty(encoded)) return;
        var parts = encoded.Split(',');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var w) || !int.TryParse(parts[1], out var h) ||
            w <= 0 || h <= 0 || w > SystemInfo.maxTextureSize || h > SystemInfo.maxTextureSize) return;
        var pixels = System.Convert.FromBase64String(parts[2]);
        if (pixels.LongLength != (long)w * h * 4) return;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.LoadRawTextureData(pixels);
        texture.Apply(false, true);
        var view = new GameObject("小雕替身");
        view.transform.SetParent(enemy.transform, false);
        view.transform.localPosition = Vector3.up;
        view.transform.rotation = Camera.main.transform.rotation;
        birdSprite = view.AddComponent<SpriteRenderer>();
        float width = Camera.main.orthographicSize * 9f / 16f;
        birdSprite.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(.5f, .5f), texture.width / width);
        SelectOpponent(null);
    }

    private void SelectOpponent(GameObject model)
    {
        if (model && !boss) boss = Instantiate(model, enemy.transform, false);
        if (boss) boss.SetActive(model != null);
        bird.gameObject.SetActive(!model && !birdSprite);
        if (birdSprite) birdSprite.gameObject.SetActive(!model);
        Camera.main.orthographicSize = model ? 5.9f : 4.86f;
    }

    [Preserve]
    public void Cast(string sourceId)
    {
        if (!int.TryParse(sourceId, out var id)) return;
        var skill = System.Array.Find(skills, item => item && item.sourceId == id);
        if (!skill || hero.mData == null || enemy.mData == null) return;
        SelectOpponent(skill.previewEnemy);
        var caster = skill.previewAsOpponent ? enemy : hero;
        var target = skill.previewAsOpponent ? hero : enemy;
        // Encounter previews repeat. Restore the dummy before the next cast so it stays targetable.
        if (target.GetAttributeFloat("chp") < 8f) target.SetAttributeValue("chp", 50f);
        var go = new GameObject(skill.name);
        var damage = go.AddComponent<CombatDamage>();
        damage.PhysicsDetection = false;
        damage.mGraph = skill;
        damage.Init();
        if (!damage.inited) { Destroy(go); return; }
        var playback = SkillPlayback.On(damage);
        float last = 0;
        foreach (var action in skill.RootSet.Actions) last = Mathf.Max(last, action.WaitTime);
        playback.RetireAfter(1 + last);
        playback.OriginalProjectileImpact = (source, victim) => SkillPlayback.ApplyIncomingDamage(source, victim, 1f);
        damage.DealDamage(caster.mData, target.mData);
        Debug.Log("Qingyun cast " + sourceId);
    }

    [Preserve]
    public void Pause(string unused) { Time.timeScale = 0; }

    private void Update()
    {
        if (previewCaster && Time.unscaledTime >= nextReadout)
        {
            nextReadout = Time.unscaledTime + .25f;
            Report("state");
        }
        elapsed += Time.deltaTime;
        if (!ferry || enemy.mData == null || !enemy.mData.AvailableForInteraction) return;
        float approach = Mathf.SmoothStep(0, 1, (elapsed - .7f) / 2.05f);
        float retreat = Mathf.SmoothStep(0, 1, (elapsed - 3.5f) / 1.9f);
        var dive = new Vector3(.44f, -.54f, -.6f) * approach * (1 - retreat);
        enemy.SetPosition(center + Vector3.forward * 4 + dive + new Vector3(4, 4, 1) * retreat, false);
    }
}
