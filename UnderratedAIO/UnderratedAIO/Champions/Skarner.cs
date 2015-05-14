﻿using System;
using System.Collections.Generic;
using System.Linq;
using Color = System.Drawing.Color;
using LeagueSharp;
using LeagueSharp.Common;
using UnderratedAIO.Helpers;
using Environment = UnderratedAIO.Helpers.Environment;
using Orbwalking = UnderratedAIO.Helpers.Orbwalking;

namespace UnderratedAIO.Champions
{
    internal class Skarner
    {
        public static Menu config;
        public static Orbwalking.Orbwalker orbwalker;
        public static AutoLeveler autoLeveler;
        public static Spell Q, W, E, R;
        public static readonly Obj_AI_Hero player = ObjectManager.Player;

        public Skarner()
        {
            if (player.BaseSkinName != "Skarner")
            {
                return;
            }
            InitSkarner();
            InitMenu();
            Game.PrintChat("<font color='#9933FF'>Soresu </font><font color='#FFFFFF'>- Skarner</font>");
            Drawing.OnDraw += Game_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Helpers.Jungle.setSmiteSlot();
            Utility.HpBarDamageIndicator.DamageToUnit = ComboDamage;
            Obj_AI_Base.OnProcessSpellCast += Game_ProcessSpell;
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            switch (orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo();
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass();
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    Clear();
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    break;
                default:
                    break;
            }
            Jungle.CastSmite(config.Item("useSmite").GetValue<KeyBind>().Active);
            if (config.Item("QSSEnabled").GetValue<bool>())
            {
                ItemHandler.UseCleanse(config);
            }
        }

        private void Combo()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
            if (target == null)
            {
                return;
            }
            if (config.Item("selected").GetValue<bool>())
            {
                target = CombatHelper.SetTarget(target, TargetSelector.GetSelectedTarget());
                orbwalker.ForceTarget(target);
            }
            if (config.Item("useItems").GetValue<bool>())
            {
                ItemHandler.UseItems(target, config, ComboDamage(target));
            }
            if (SkarnerR)
            {
                return;
            }
            var dist = player.Distance(target);
            if (config.Item("useq").GetValue<bool>() && Q.CanCast(target))
            {
                Q.Cast(config.Item("packets").GetValue<bool>());
            }
            if (config.Item("usew").GetValue<bool>() || player.Distance(target) < 600)
            {
                W.Cast(config.Item("packets").GetValue<bool>());
            }
            if (config.Item("usee").GetValue<bool>() && E.CanCast(target) &&
                ((dist < config.Item("useeMaxRange").GetValue<Slider>().Value &&
                  dist > config.Item("useeMinRange").GetValue<Slider>().Value) || target.Health < ComboDamage(target)))
            {
                E.Cast(target, config.Item("packets").GetValue<bool>());
            }
            if (config.Item("user").GetValue<bool>() && R.CanCast(target) &&
                (!config.Item("ult" + target.SkinName).GetValue<bool>() || player.CountEnemiesInRange(1500) == 1) &&
                CountPassive(target) >= config.Item("userstacks").GetValue<Slider>().Value)
            {
                R.Cast(target, config.Item("packets").GetValue<bool>());
            }
            var ignitedmg = (float) player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
            bool hasIgnite = player.Spellbook.CanUseSpell(player.GetSpellSlot("SummonerDot")) == SpellState.Ready;
            if (config.Item("useIgnite").GetValue<bool>() && ignitedmg > target.Health && hasIgnite &&
                !E.CanCast(target))
            {
                player.Spellbook.CastSpell(player.GetSpellSlot("SummonerDot"), target);
            }
        }

        private void Clear()
        {
            float perc = config.Item("minmana").GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            MinionManager.FarmLocation bestPositionE =
                E.GetLineFarmLocation(MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.NotAlly));
            var qMinions = Environment.Minion.countMinionsInrange(player.Position, Q.Range);
            if (config.Item("useeLC").GetValue<bool>() && E.IsReady() &&
                bestPositionE.MinionsHit > config.Item("ehitLC").GetValue<Slider>().Value)
            {
                E.Cast(bestPositionE.Position, config.Item("packets").GetValue<bool>());
            }
            if (config.Item("useqLC").GetValue<bool>() && Q.IsReady() &&
                qMinions >= config.Item("qhitLC").GetValue<Slider>().Value)
            {
                Q.Cast(config.Item("packets").GetValue<bool>());
            }
        }

        private void Harass()
        {
            float perc = config.Item("minmanaH").GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            Obj_AI_Hero target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            if (target == null)
            {
                return;
            }
            if (config.Item("useqH").GetValue<bool>() && Q.CanCast(target))
            {
                Q.Cast(config.Item("packets").GetValue<bool>());
            }
            if (config.Item("useeH").GetValue<bool>() && E.CanCast(target))
            {
                E.Cast(target, config.Item("packets").GetValue<bool>());
            }
        }

        private static float ComboDamage(Obj_AI_Hero hero)
        {
            double damage = 0;
            if (Q.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.Q);
            }
            if (E.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.E);
            }
            if (R.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.R) * 2;
            }
            damage += ItemHandler.GetItemsDamage(hero);
            var ignitedmg = player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            if (player.Spellbook.CanUseSpell(player.GetSpellSlot("summonerdot")) == SpellState.Ready &&
                hero.Health < damage + ignitedmg)
            {
                damage += ignitedmg;
            }
            return (float) damage;
        }

        private void Game_ProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (config.Item("useragainstpush").GetValue<bool>() &&
                orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                var spellName = args.SData.Name;
                if (spellName == "TristanaR" || spellName == "BlindMonkRKick" || spellName == "AlZaharNetherGrasp" ||
                    spellName == "GalioIdolOfDurand" || spellName == "VayneCondemn" ||
                    spellName == "JayceThunderingBlow" || spellName == "Headbutt")
                {
                    if (args.Target.IsMe && R.CanCast(sender) &&
                        (!config.Item("ult" + sender.SkinName).GetValue<bool>() || player.CountEnemiesInRange(1500) == 1))
                    {
                        R.Cast(sender, config.Item("packets").GetValue<bool>());
                    }
                }
            }
        }

        private void Game_OnDraw(EventArgs args)
        {
            DrawHelper.DrawCircle(config.Item("drawqq", true).GetValue<Circle>(), Q.Range);
            DrawHelper.DrawCircle(config.Item("drawee", true).GetValue<Circle>(), E.Range);
            DrawHelper.DrawCircle(config.Item("drawrr", true).GetValue<Circle>(), R.Range);
            Helpers.Jungle.ShowSmiteStatus(
                config.Item("useSmite").GetValue<KeyBind>().Active, config.Item("smiteStatus").GetValue<bool>());
            Utility.HpBarDamageIndicator.Enabled = config.Item("drawcombo").GetValue<bool>();
        }

        private void InitSkarner()
        {
            Q = new Spell(SpellSlot.Q, 325);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 985);
            E.SetSkillshot(
                E.Instance.SData.SpellCastTime, E.Instance.SData.LineWidth, E.Speed, false, SkillshotType.SkillshotLine);
            R = new Spell(SpellSlot.R, 325);
        }

        private static bool SkarnerR
        {
            get { return player.Buffs.Any(buff => buff.Name == "SkarnerImpale"); }
        }

        private static bool SkarnerW
        {
            get { return player.Buffs.Any(buff => buff.Name == "SkarnerExoskeleton"); }
        }

        private static int CountPassive(Obj_AI_Base target)
        {
            var passive = target.Buffs.FirstOrDefault(buff => buff.Name == "skarnerpassivebuff");
            return passive != null ? passive.Count : 0;
        }

        private void InitMenu()
        {
            config = new Menu("Skarner", "Skarner", true);
            // Target Selector
            Menu menuTS = new Menu("Selector", "tselect");
            TargetSelector.AddToMenu(menuTS);
            config.AddSubMenu(menuTS);
            // Orbwalker
            Menu menuOrb = new Menu("Orbwalker", "orbwalker");
            orbwalker = new Orbwalking.Orbwalker(menuOrb);
            config.AddSubMenu(menuOrb);
            // Draw settings
            Menu menuD = new Menu("Drawings ", "dsettings");
            menuD.AddItem(new MenuItem("drawqq", "Draw Q range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawee", "Draw E range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawrr", "Draw R range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawcombo", "Draw combo damage")).SetValue(true);
            config.AddSubMenu(menuD);
            Menu menuC = new Menu("Combo ", "csettings");
            menuC.AddItem(new MenuItem("useq", "Use Q")).SetValue(true);
            menuC.AddItem(new MenuItem("usew", "Use W")).SetValue(true);
            menuC.AddItem(new MenuItem("usee", "Use E")).SetValue(true);
            menuC.AddItem(new MenuItem("useeMinRange", "   E min Range")).SetValue(new Slider(150, 0, (int) E.Range));
            menuC.AddItem(new MenuItem("useeMaxRange", "   E max Range")).SetValue(new Slider(800, 0, (int) E.Range));
            menuC.AddItem(new MenuItem("user", "Use R")).SetValue(true);
            menuC.AddItem(new MenuItem("userstacks", "   R min stack")).SetValue(new Slider(3, 0, 3));
            menuC.AddItem(new MenuItem("useragainstpush", "Use R to counter spells")).SetValue(true);
            var sulti = new Menu("TeamFight Ult block", "dontult");
            foreach (var hero in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsEnemy))
            {
                sulti.AddItem(new MenuItem("ult" + hero.SkinName, hero.SkinName)).SetValue(false);
            }
            menuC.AddSubMenu(sulti);
            menuC.AddItem(new MenuItem("selected", "Focus Selected target")).SetValue(true);
            menuC.AddItem(new MenuItem("useIgnite", "Use Ignite")).SetValue(true);
            menuC = ItemHandler.addItemOptons(menuC);
            config.AddSubMenu(menuC);
            // Harass Settings
            Menu menuH = new Menu("Harass ", "Hsettings");
            menuH.AddItem(new MenuItem("useqH", "Use Q")).SetValue(true);
            menuH.AddItem(new MenuItem("useeH", "Use E")).SetValue(true);
            menuH.AddItem(new MenuItem("minmanaH", "Keep X% mana")).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuH);
            // LaneClear Settings
            Menu menuLC = new Menu("LaneClear ", "Lcsettings");
            menuLC.AddItem(new MenuItem("useqLC", "Use Q")).SetValue(true);
            menuLC.AddItem(new MenuItem("qhitLC", "   Min hit").SetValue(new Slider(2, 1, 10)));
            menuLC.AddItem(new MenuItem("useeLC", "Use E")).SetValue(true);
            menuLC.AddItem(new MenuItem("ehitLC", "   Min hit").SetValue(new Slider(2, 1, 10)));
            menuLC.AddItem(new MenuItem("minmana", "Keep X% mana")).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuLC);
            Menu menuM = new Menu("Misc ", "Msettings");
            menuM = Jungle.addJungleOptions(menuM);
            menuM = ItemHandler.addCleanseOptions(menuM);

            Menu autolvlM = new Menu("AutoLevel", "AutoLevel");
            autoLeveler = new AutoLeveler(autolvlM);
            menuM.AddSubMenu(autolvlM);

            config.AddSubMenu(menuM);
            config.AddItem(new MenuItem("packets", "Use Packets")).SetValue(false);
            config.AddItem(new MenuItem("UnderratedAIO", "by Soresu v" + Program.version.ToString().Replace(",", ".")));
            config.AddToMainMenu();
        }
    }
}