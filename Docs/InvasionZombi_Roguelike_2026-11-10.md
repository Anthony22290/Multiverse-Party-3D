# Invasión Zombi · versión roguelike (2026-11-10)

## Mapa más grande con sectores
- La plaza pasa de 48×48 m a 80×80 m (`SurvivalLevel.Half` 24 → 40). Mantiene la camioneta en el centro y las cuatro puertas de entrada.
- Cuatro sectores con su decorado, letrero y luz:
  - **El Desguace** (NE): coches averiados y neumáticos.
  - **El Almacén** (NO): contenedores y palés.
  - **La Gasolinera** (SE): cisterna, barriles y tuberías.
  - **Parque de Bomberos** (SO).
- **Piezas custodiadas** (`SurvivalSectors.cs`, `SurvivalCache`):
  - En las oleadas 2, 5 y 7 la pieza no la lleva un zombi brillante: está en el cofre de un sector, con un haz dorado y entre 3 y 6 guardianes (esqueletos, blobs, lanzabombas, fantasmas, brutos).
  - Los guardianes esperan quietos hasta que alguien se acerca a menos de 13 m o les golpea.
  - Con los guardianes eliminados, quedarse junto al cofre durante 1,2 s lo abre y suelta la pieza.
  - Un indicador en el borde de la pantalla marca el sector, la distancia y los guardianes que quedan.
  - Los bots van a por ella.
- **8 cajas de suministros** repartidas por el mapa: quedarse al lado 0,8 s las abre y sueltan botiquín, furia o bomba. Se rellenan 2 en cada descanso.

## Enemigos nuevos (`Resources/SurvivalMonsters`, preparados con `Editor/SurvivalMonsterSetup.cs`)
| Enemigo | Modelo | Comportamiento |
|---|---|---|
| Esqueleto | Esqueleto pirata | Se rearma a los 2,5 s si no lo rematas con el golpe fuerte del combo o una bomba |
| Lanzabombas | Tribal (monstruos) | Guarda 7–13 m y lanza bombas en parábola con círculo rojo; la explosión daña también a los zombis |
| Blob / Blobito | GreenBlob | Al morir se divide en dos blobitos rápidos |
| Fantasma | Ghost | Flota y es intangible 1,3 s de cada 5 (parpadea) |
| Bruto | Yeti | 330 de vida y golpes que mandan muy lejos |

Aparecen de forma progresiva desde la oleada 2 (los lanzabombas tienen un máximo de 3 por oleada y los brutos de 1).

## Capa roguelike
- **Mejoras en cada descanso** (`SurvivalPerks.cs`): eliges 1 de 3 al azar con 1/2/3 o un clic. Son acumulables hasta nivel IV y los bots eligen solos.
  - Fuerza, Armadura, Velocidad, Alcance, Vampiro, Regeneración.
  - Onda de choque, Suerte, Cadáveres explosivos, Espinas.
  - Las mejoras activas se ven abajo en el HUD.
- **Modificadores de oleada** desde la 2: Horda veloz, Lluvia de bombas, Noche cerrada, Oleada de esqueletos, Invasión de monstruos y Brutos sueltos. En las oleadas de jefe solo pueden salir la horda veloz o la noche.
- Dificultad: los zombis ganan un 9 % de vida y un 6 % de daño por oleada. Los objetos soltados bajan a: botiquín 6 % (cura 35), furia 3 %, bomba 2 %.

## Pruebas
- `SurvivalPlaytest.Run4` y `Run2`: PASS, con 10/10 oleadas, los 4 jefes, las piezas custodiadas de las oleadas 2, 5 y 7 recuperadas y el escape completado.
- `ZombieRogueShots`: los 5 enemigos nuevos con modelo y animación, lanzamiento de bomba, cofre con 5 guardianes e indicador, y cartas de mejora. Capturas `Docs/Claude_Capturas/Minijuegos/R*.png`.
- Consola sin errores.

## Pendiente
Con la IA controlando también a tu personaje no cayó nadie en ninguna partida de prueba, pero la IA esquiva mejor que un jugador real. Hay que probar a mano y ajustar si hace falta el daño por oleada (`SurvivalZombie`, 0,06), la cantidad de lanzabombas y la frecuencia de objetos.
