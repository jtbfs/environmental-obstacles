# What's this?

This is a mod for custom predators in The Bibites! They are used for competition, or evolution purposes.

# Elaborate...?

To be more precise, the mod itself is really simple (you can look at the code in `Mod.cs`), it simply only affects any bibites with the tag "`333immortal`" assigned to them.

This tag has the following custom effects, causing the bibite to be:
- Ungrabbable
- Unkillable
- Automatically locked to its zone on initial spawn/birth
  - (unless outside of any zones, then it remains homeless, and follows the center instead)
- Instantly kill a bibite on grab
- Bite every 0.05 seconds
- Completely ignorant of all food, solely interested in targeting bibites only
-------------------
Additionally, the tag also has specific effects depending on the color of the bibite:
- Unique targeting:
  - Purple (`1.000`, `0.500`, `1.000`): Targets the bibite with the most maturity
  - White (`0.000`, `0.000`, `0.000`): Targets the bibite with a mixed of maturity, and distance
    - Formula: (maturity × (maturity + 1)) / (distance² × 0.02 + 1)
  - Blue (`0.000`, `0.000`, `1.000`): No targeting, rams through the center in a line, back and forth repeatedly
  - Fallback (`Default`): Targets the closest bibite
-------------------
This makes it a great way to train the bibites against the "perfect predators", and you can customize it yourself, setting their speed as low as you need it to be. Or allowing them to grab at all times, causing them to instakill the prey on grab. Setting their jaw strength to be low, etc.

You have a lot of ways you can do to customize this pressure.
