# Referencias de mecánicas para Arena del Caos

Inspección estática con REA 6.8.0, 2026-11-03. No se ejecutaron los juegos, no se importaron modelos, sonidos, animaciones ni código a Assets. Los nombres observados permiten identificar subsistemas; no demuestran el comportamiento ni los parámetros de una partida.

## Evidencia conservada

- Gang Beasts: `gang-beasts-metadata.evidence.json`, evidencia `ev_44bfbaed1d6a84f2613522a61df1ae0a2f6ee93fde029d1aa1407c726b783ced`. Archivo suministrado `C:/Users/antho/Downloads/Gang Beasts b20270933_ELENEMIGOS/Gang Beasts b20270933_ELENEMIGOS/Gang Beasts_Data/il2cpp_data/Metadata/global-metadata.dat`. SHA-256 `830a22716fe7bbec162daecd00738c4effd9609d1e512c8aedb8f091e338a2f0`, metadatos IL2CPP v29, 16.046 tipos y 104.163 métodos. Inventario completo de metadatos, sin enlazar GameAssembly.dll: tipos de parámetros, constantes, implementación nativa y ejecución no observados.
- Human: Fall Flat: `human-fall-flat-managed.evidence.json`, identidad `ev_0ece66cc4ebdc920afea5ee1d07dde8ae979165034ee04abd931bca0d66c3862`; `human-fall-flat-members.evidence.json`, miembros `ev_9d6b7346caec412c807bc416edc062bbaaf52ce04694d9ebaec49ebffd25fc02`. Archivo `D:/Juegos/Human Fall Flat/Human_Data/Managed/Assembly-CSharp.dll`, SHA-256 `44914a5c7f45ccd13368f99fb2a0773591c97d24b634fa1bf756bd2ef795cbec`, MVID `963ef783-b66f-4f7f-8dd2-c480a565da1b`. Selección exacta de cinco tipos: HumanControls, HandMuscles, LegMuscles, TorsoMuscles y Ragdoll; 146 campos y 68 métodos. Identidad coincide con la inspección anterior descrita en el chat. Sin ejecución ni reconstrucción completa.

## Declaraciones observadas

| Referencia | Declaraciones comprobadas | Alcance de la conclusión |
| --- | --- | --- |
| Gang Beasts: agarre | Femur.BodyHandeler: leftGrabJoint, rightGrabJoint, ResetLeftGrab, ResetRightGrab; CollisionHandeler: LeftGrab, RightGrab, GrabCheck | Existe separación de agarres por mano. Fuerzas y duración desconocidas. |
| Gang Beasts: combate | ControlHandeler: PunchGrabCheck, KickStompCheck, LiftCheck, ClimbCheck; campos leftPunch/rightPunch, lift/liftSelf, grabJump, headbutt; MovementHandeler: ArmActionPunching, ArmActionGrabbing, HeadActionHeadbutting, LegActionKicking | Existen acciones diferenciadas de golpe, agarre, elevación, escalada y cabezazo/patada. No se ha comprobado su mapa de controles. |
| Gang Beasts: KO | StatusHandeler: AddDamage, UpdateHealth, SetMaxUnconsciousTime, UpdateGrab; campos healthDamage, staminaDamage, healthRegeneration, knockoutThreshold, unconsciousTime, minUnconsciousTime, maxUnconsciousTime; MovementHandeler.Unconscious | Hay estado inconsciente y parámetros de daño, regeneración y tiempo. Su fórmula no se ha recuperado. |
| Human: Fall Flat: manos | HumanControls: leftExtend/rightExtend, leftGrab/rightGrab, cameraPitchAngle; HandMuscles: ProcessHand, LiftBody, FindTarget, maxLiftForce, grabMaxForce, climbMaxForce | Manos independientes y subsistema físico de agarre/elevación con límites declarados. |
| Human: Fall Flat: locomoción | LegMuscles: AddWalkForce, ApplyJumpImpulses, framesToApplyJumpImpulse, forwardImpulse/upImpulse; TorsoMuscles: StandAnimation, ClimbAnimation, JumpAnimation, FallAnimation, FreeFallAnimation, StabilizeHorizontal | Se distinguen soporte, salto, caída y escalada; no se infiere equivalencia física con la implementación de Arena. |
| Human: Fall Flat: cuerpo | Ragdoll: segmentos de cabeza, torso, cadera, manos, pies y bola; AddAntistretch, SetupColliders, StretchHandsLegs | Existen construcción por segmentos y función antiextensión; no se copió su implementación. |

## Propuesta original para Arena del Caos

Estas son decisiones de diseño propias, no valores extraídos de los juegos:

1. Mantener agarre izquierdo/derecho, mirar para orientar manos, levantar y trepar de la implementación actual.
2. Separar daño de equilibrio, KO temporal, recuperación y eliminación definitiva: un KO en cubierta no declara derrota; salir del escenario sí. Resolver ganador después de procesar todas las eliminaciones del paso.
3. Acumular daño hasta KO y regenerarlo al dejar de recibir golpes; recuperación con un tiempo acotado y breve protección contra encadenamiento infinito.
4. Facilitar cargar a un rival inconsciente; resistir el levantamiento cuando está consciente. Lanzamiento intencional con F, sin añadir impulso al soltar agarres.
5. Amortiguar seguimiento del ragdoll y no aplicar corrección vertical de postura como si estuviera apoyado cuando está en caída libre. Salto solo con apoyo o situación de agarre válida; evitar impulsos duplicados y autoagarres.
6. Comprobar en Unity salto, caída alta, aterrizaje, KO/recuperación, elevación/lanzamiento y ganador único/empate. La inspección estática de referencia no sustituye esas pruebas.

Estado de investigación: identidad y declaraciones de ambos juegos comprobadas; combinación propuesta; comportamiento original, controles exactos, tiempos y fuerzas exactas sin verificar. La implementación y las pruebas de Arena se documentan por separado.
