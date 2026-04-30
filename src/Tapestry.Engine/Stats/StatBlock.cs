// src/Tapestry.Engine/Stats/StatBlock.cs
namespace Tapestry.Engine.Stats;

public class StatBlock
{
    // --- Base attributes ---
    private int _baseStrength;
    private int _baseIntelligence;
    private int _baseWisdom;
    private int _baseDexterity;
    private int _baseConstitution;
    private int _baseLuck;

    public int BaseStrength
    {
        get => _baseStrength;
        set { _baseStrength = value; _dirty = true; }
    }

    public int BaseIntelligence
    {
        get => _baseIntelligence;
        set { _baseIntelligence = value; _dirty = true; }
    }

    public int BaseWisdom
    {
        get => _baseWisdom;
        set { _baseWisdom = value; _dirty = true; }
    }

    public int BaseDexterity
    {
        get => _baseDexterity;
        set { _baseDexterity = value; _dirty = true; }
    }

    public int BaseConstitution
    {
        get => _baseConstitution;
        set { _baseConstitution = value; _dirty = true; }
    }

    public int BaseLuck
    {
        get => _baseLuck;
        set { _baseLuck = value; _dirty = true; }
    }

    // --- Base vital caps ---
    private int _baseMaxHp;
    private int _baseMaxResource;
    private int _baseMaxMovement;

    public int BaseMaxHp
    {
        get => _baseMaxHp;
        set { _baseMaxHp = value; _dirty = true; }
    }

    public int BaseMaxResource
    {
        get => _baseMaxResource;
        set { _baseMaxResource = value; _dirty = true; }
    }

    public int BaseMaxMovement
    {
        get => _baseMaxMovement;
        set { _baseMaxMovement = value; _dirty = true; }
    }

    // --- Current vitals ---
    private int _hp;
    private int _resource;
    private int _movement;

    public int Hp
    {
        get => _hp;
        set { _hp = Math.Clamp(value, 0, MaxHp); }
    }

    public int Resource
    {
        get => _resource;
        set { _resource = Math.Clamp(value, 0, MaxResource); }
    }

    public int Movement
    {
        get => _movement;
        set { _movement = Math.Clamp(value, 0, MaxMovement); }
    }

    // --- Effective values (base + modifiers, cached) ---
    private int _cachedStrength;
    private int _cachedIntelligence;
    private int _cachedWisdom;
    private int _cachedDexterity;
    private int _cachedConstitution;
    private int _cachedLuck;
    private int _cachedMaxHp;
    private int _cachedMaxResource;
    private int _cachedMaxMovement;
    private bool _dirty = true;

    public int Strength { get { Recalculate(); return _cachedStrength; } }
    public int Intelligence { get { Recalculate(); return _cachedIntelligence; } }
    public int Wisdom { get { Recalculate(); return _cachedWisdom; } }
    public int Dexterity { get { Recalculate(); return _cachedDexterity; } }
    public int Constitution { get { Recalculate(); return _cachedConstitution; } }
    public int Luck { get { Recalculate(); return _cachedLuck; } }
    public int MaxHp { get { Recalculate(); return _cachedMaxHp; } }
    public int MaxResource { get { Recalculate(); return _cachedMaxResource; } }
    public int MaxMovement { get { Recalculate(); return _cachedMaxMovement; } }

    // --- Modifiers ---
    private readonly List<StatModifier> _modifiers = new();

    public IReadOnlyList<StatModifier> Modifiers => _modifiers.AsReadOnly();

    public void AddModifier(StatModifier modifier)
    {
        _modifiers.Add(modifier);
        _dirty = true;
    }

    public void RemoveModifiersBySource(string source)
    {
        if (_modifiers.RemoveAll(m => m.Source == source) > 0)
        {
            _dirty = true;
        }
    }

    // --- Cache recalculation ---
    private void Recalculate()
    {
        if (!_dirty)
        {
            return;
        }

        _cachedStrength = _baseStrength;
        _cachedIntelligence = _baseIntelligence;
        _cachedWisdom = _baseWisdom;
        _cachedDexterity = _baseDexterity;
        _cachedConstitution = _baseConstitution;
        _cachedLuck = _baseLuck;
        _cachedMaxHp = _baseMaxHp;
        _cachedMaxResource = _baseMaxResource;
        _cachedMaxMovement = _baseMaxMovement;

        foreach (var mod in _modifiers)
        {
            switch (mod.Stat)
            {
                case StatType.Strength: _cachedStrength += mod.Value; break;
                case StatType.Intelligence: _cachedIntelligence += mod.Value; break;
                case StatType.Wisdom: _cachedWisdom += mod.Value; break;
                case StatType.Dexterity: _cachedDexterity += mod.Value; break;
                case StatType.Constitution: _cachedConstitution += mod.Value; break;
                case StatType.Luck: _cachedLuck += mod.Value; break;
                case StatType.MaxHp: _cachedMaxHp += mod.Value; break;
                case StatType.MaxResource: _cachedMaxResource += mod.Value; break;
                case StatType.MaxMovement: _cachedMaxMovement += mod.Value; break;
            }
        }

        // Re-clamp current vitals after max changes
        _hp = Math.Clamp(_hp, 0, _cachedMaxHp);
        _resource = Math.Clamp(_resource, 0, _cachedMaxResource);
        _movement = Math.Clamp(_movement, 0, _cachedMaxMovement);

        _dirty = false;
    }

    /// <summary>Marks the cache dirty — call after changing a Base* property.</summary>
    public void Invalidate()
    {
        _dirty = true;
    }
}
