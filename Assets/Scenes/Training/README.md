# Training baked lighting

- Four directional lightmaps store static sunlight bounce; 2,048 light probes illuminate moving objects.
- Five baked reflection probes capture the arena. Sunlight and direct shadows remain real-time.
- The custom sand terrain has no terrain layers, so Unity cannot bake it directly. `Sand GI Bake Proxy (Editor Bake Only)` supplies its geometry during baking; its renderer stays **disabled during gameplay** and it has no collider.
- Sand and tyre grooves share the proxy's world-aligned indirect lightmap through `Training Sand.mat`. Do not remove those hidden material bindings.

After changing static scenery, terrain or the sun, save Training and use **Death Derby → Lighting → Rebake Training Lighting**. This command temporarily enables the proxy, restores the visible terrain afterward, and updates the sand's lightmap bindings. Using Unity's ordinary Generate Lighting button alone will not perform that setup.

Moving objects receive the baked lighting but do not update its bounce or reflection captures. Coop's lighting data is independent and has not been modified.
