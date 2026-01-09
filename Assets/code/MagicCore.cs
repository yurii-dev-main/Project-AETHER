using UnityEngine;
using System.Collections.Generic;

// --- 1. ОПРЕДЕЛЕНИЯ (СЛОВАРЬ) ---

// Структура "Чертеж Заклинания" (Тот самый Трафарет)


// --- 2. МАТРИЦА РЕАКЦИЙ ---

public static class ElementalMatrix
{

    // Структура результата реакции
    public struct ReactionResult
    {
        public string Name; // "Steam Explosion", "Freeze", "Melt"
        public float DamageMultiplier;
        public string EffectTag; // Тэг для спавна эффектов
    }

    // Главный метод: Что будет, если Element A попадет в Element B?
    public static ReactionResult CalculateReaction(Element incoming, Element existing)
    {
        ReactionResult res = new ReactionResult { Name = "Hit", DamageMultiplier = 1f, EffectTag = "None" };

        // --- ОГОНЬ ---
        if (incoming == Element.Fire)
        {
            if (existing == Element.Ice)
            {
                res.Name = "MELT";
                res.DamageMultiplier = 1.5f; // Огонь по льду больно
                res.EffectTag = "WaterPuddle"; // Лед тает в воду
            }
            else if (existing == Element.Water)
            {
                res.Name = "VAPORIZE"; // Паровой взрыв
                res.DamageMultiplier = 0.5f; // Урон гасится
                res.EffectTag = "SteamCloud"; // Создает облако (Туман)
            }
            else if (existing == Element.Earth)
            {
                res.Name = "SCORCH";
                res.DamageMultiplier = 1.0f;
            }
        }

        // --- ВОДА ---
        else if (incoming == Element.Water)
        {
            if (existing == Element.Fire)
            {
                res.Name = "EXTINGUISH";
                res.DamageMultiplier = 2.0f; // Вода убивает огненных элементалей
            }
        }

        // --- ЛЕД ---
        else if (incoming == Element.Ice)
        {
            if (existing == Element.Water)
            {
                res.Name = "FREEZE";
                res.EffectTag = "IceBlock"; // Вода замерзает в лед
            }
        }

        return res;
    }
}