# FlowLights

A traffic-management game prototype in Godot 4.7 C#. You untangle Riverside one junction at a time. Each level focuses on a single junction: traffic enters from the edge of the playable area, and you use simple tools until the junction's flow stays above 90% for 45 seconds. Then the next junction joins the network, so cars from a finished junction now drive into the new one. Finished junctions stay editable.

## Running

Open the folder in Godot 4.7 (.NET), build, and press Play. The main scene is `Scenes/Main.tscn`.

| Input | Action |
| --- | --- |
| Toolbar (bottom) | Pick a tool. Hover a button to see what it does and what it costs |
| Left click | Use the tool (inspect, place a sign, add a lane, raise a speed limit, ...) |
| Right click | Undo-style secondary action: remove a lane, lower a speed limit, stop editing a lane |
| Esc | Close popup, stop editing, back to Inspect |
| Right/middle drag, WASD | Pan |
| Mouse wheel, Q / E, R / F | Zoom, rotate, tilt |
| Space, 1-3 | Pause, normal, fast, faster |

Tools unlock per level: signs, zebra crossings and lane arrows, then turn lanes, traffic lights, speed limits and one-ways, a tow-truck dispatch (crashes clear in 20 s instead of 75 s), detectors and smart lights, and cameras.

Traffic lights have a signal head per lane with an arrow for each direction that lane can take, and a coloured stop bar per lane. Select a light to set the green time of each phase. From level 5 the turn-phases upgrade lets you choose, per phase, which turns of which road get green (for example a separate phase for left turners), and add or remove phases.

Lights cost money to run: €15 a minute for a fixed light, €20 for an actuated one and €25 for an adaptive one. Detectors add €3 and cameras €4. The money panel shows net income per minute (road tax and fines minus running costs), so lights everywhere eat into your income.

## Architecture

`Scripts/Core` is plain C# with no Godot dependency, so the whole game can run headless.

- `Network`: junctions, roads (lane counts per direction), lanes, connectors (turn paths) with conflict points. `RoadNetwork.Activate` decides which junctions are active, which are boundaries where traffic spawns and despawns, and which are dormant.
- `Control`: `SignControl` (priority, yield, stop, or priority to the right per approach) and `SignalControl` with fixed, detector-actuated and adaptive variants.
- `Drivers`, `Vehicles`: driver archetypes with speed preference, compliance, headway, gap acceptance and patience. Car following uses the Intelligent Driver Model.
- `Simulation`: `TrafficSimulation` steps the world, `RightOfWayArbiter` grants junction entry, `CollisionDetector` turns bad decisions into crashes, `CityServices` holds the dispatch.
- `Routing`: lane-level reverse Dijkstra per destination with live congestion.
- `Campaign`: `CampaignProgress` runs the level chain, warm-up, the hold timer and tool unlocks.
- `Editing`: `CityEditor` is the only way the player changes the city. Each action checks unlocks, the level and the price, and returns an `EditResult`.
- `Pedestrians`: a crosswalk at every road end of an active junction. Walkers on a zebra go first, at a light they walk when their road is red, and otherwise they wait for a gap (or jaywalk once they run out of patience). Cars turning across an occupied half of a crosswalk wait.
- Bicycles are a slow vehicle type that rides in mixed traffic. They pay no road tax.
- `Economy`: treasury, happiness-based road tax, fines and upkeep.
- `Maps`: JSON map format (`Data/Maps/*.json`) with junctions, roads, levels (focus junction, intro, unlocks, demand) and decorative areas.

`Scripts/Presentation` is the Godot layer. `GameRoot` is the composition root. `Rendering` draws roads, sign posts, lane arrows and overlays. `Interaction` holds the camera, picking and the `ToolController`. `Interface` holds the level card, toolbar, clock, info card, sign picker and the code-drawn icons.

## Balancing

`Tools/Headless` runs the simulation without Godot:

```
cd Tools/Headless
dotnet run -- ../../Data/Maps/riverside.json 12 3 fix 1 -v
```

Arguments are the map, minutes, the level to jump to (earlier levels are fixed automatically), a strategy for that level (`none`, `signs`, or `fix` for the intended solution) and a seed. `-v` prints every minute. Set `DEMAND=13,26[,walkersPerMinute,cyclistShare]` to try another demand, and `FIX=lanes|light|light0|lightleft|oneway|smart|phases` to try another solution. `DIAG=1` prints queues per lane every minute, `DIAG=2` prints a summary of why the front car of each lane was waiting and what blocked it.

Flow is based on the average delay per car at the focus junction: 5 s or less is 100%, about 12 s is 90%, and 75 s is 0%. At a traffic light the first 10 s of delay are free instead of 5 s, since waiting at red is expected (traffic engineers rate signalised junctions the same way). Crashes temporarily lower it. Each level was tuned so that signs alone stay under 90% and the intended tool gets above it.
