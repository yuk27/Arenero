using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GeneradorTerreno : MonoBehaviour {

	public GameObject Cubo;

	public KinectSensor device;
	private Kinect.KinectInterface kinect;

	public GameObject peces;

	Terrain t;
	TerrainData tData;

	public InputField[] InputsColor;
	public InputField Vel_R;


	public float[] porcientos = {0.08f,0.1f,0.2f,0.4f,0.6f,0.8f};
	float[] porcientosDefault = {0.05f,0.07f,0.1f,0.2f,0.45f,0.55f};  //valor por defecto de distacias (se pone 

	public float alturaMaxima = 200.0f; 
	int ancho = 320; //320
	int alto  = 240; //240

	public int velRefrescado = 40;


	int tamaño; //valor del array de depths (ancho*alto)

	bool corrutina = false; //flag para saber si se esta corriendo la corrutina de creacion del mapa

	public float min; //valor minimo de reconocimiento de la arena
	public float max; //valor maximo de reconocimiento para escalar los colores a estos valores


	public float[,] alturas; //array utilizado para borrar todo el mapa de un solo por medio de un for

	[HideInInspector]
	public short[] depthImg; //array con las nuevas alturas recogidas del kinect
	short[] depthImgAnt; //alturas anteriores, su usa para ver si el cambio de alturas en un pixel es suficiente para cambiar la posicion

	float[,] pixel; //array auxiliar para cambiar un pixel a la vez de altura
	float[,,] pixelColor; ////array auxiliar para cambiar un pixel a la vez del color
	
	public float tolerancia = 0.5f; // tolerancia de diferencia entre la anterior altura y la actuqal 

	float[] splatWeights; //array que contiene las diferentes texturas que se van a usar 

	int indiceTextura = 0;  //int que decide si se usan las texturas reales o colores

//Contorno
	bool lineas;
	Color32 bandColor =  new Color32(0,0,0,255);
	Color32 bkgColor = new Color32(0, 0, 0, 0);
	int minHeight = 14000;
	int maxHeight = 20000;
	bool[,] slice;
	int bandDistance; //Number of height bands to create
	Texture2D topoMap;
//FIN Contorno


	void Start () {
		CargarDistancias (); //se cargan los valores inciales a los inputfields
		kinect = device; 
		t = Terrain.activeTerrain;
		tData = t.terrainData;
		tamaño = ancho * alto; //tamaño del array de heights
		depthImg = new short[tamaño];
		depthImgAnt = new short[tamaño];
		tData.size = new Vector3 ( tData.heightmapWidth,alturaMaxima,tData.heightmapHeight); //tamaño y el alto del terreno
		splatWeights = new float[tData.alphamapLayers]; //se inicializa el array de texturas 
		alturas = new float[ancho, alto];
		topoMap = new Texture2D( 240, 320);
		topoMap.anisoLevel = 32;
		Cubo.SetActive(lineas);
		//FIN Contorno

	}
	

	void GetDepth(short[] depthBuf) //metodo encargado de guardar los valores de alturas del kinect a un array
	{
		for (int ii = 0; ii < tamaño; ii++) {
			depthImg [ii] = (short)(depthBuf [ii] >> 3);
		}
	}

	void LimpiarTerreno(int rf){ //metodo encargado de limpiar todo el terreno 

		int x;
		int y;
		
		
		for (int i = 0; i < depthImg.Length; i++) {
			
			x = i%rf;
			y = i/rf;
			
			alturas[x,y] = 0;
			
		}
		tData.SetHeights(0, 0, alturas);

	}

	void CargarDistancias(){ //metodo encargado de cargar los inputfields con los valores iniciales
		for (int i = 0; i < porcientos.Length; i++) {
			InputsColor[i].text = porcientos[i].ToString();
		}

		Vel_R.text = velRefrescado.ToString();
	}

	public void EditarDistancias(){ //metodo encargado de editar los valores de alturas desde el inputfield

		for (int i = 0; i < porcientos.Length; i++) {
			porcientos[i] = float.Parse(InputsColor[i].text);
		}
		velRefrescado = int.Parse (Vel_R.text);
	}

	public void DefaultDistancias(){ //metodo encargado de volver a poner sus valores en default
		
		for (int i = 0; i < porcientos.Length; i++) {
			porcientos[i] =  porcientosDefault[i]	;
			CargarDistancias();
		}
		velRefrescado = 40;
		Vel_R.text = velRefrescado.ToString();
	}

	void SetColor2(float height, int x,int y){ //metodo encargado de poner el color que le corresponde a cada pixel

		for (int i = 0; i < splatWeights.Length; i++) { //se limipia el color que hubiera anteriormente
				pixelColor[x,y,i] = 0; 
		}

		float porciento = ((height- min)) /(max - min); //se saca un porcentaje de 0 a 1, 0 siendo 0 y 1 siendo el 100%

		if (porciento < porcientos[0]) {
		
			pixelColor [x, y, 0 + indiceTextura] = 1; //AZUL
			return;
		}

		if (porciento <  porcientos[1]) {
			
			pixelColor [x, y, 0 + indiceTextura] = 0.5f;  //Celeste
			pixelColor [x, y, 4] = 0.5f; 
		}

		for (int i = 1; i < porcientos.Length -1; i++) { //se pasa por cada cada posible porcentaje si el valor actual es menor se pinta del color de ese porcentaje
		
			if(porciento <= porcientos[i+1]){
			pixelColor [x, y, (i-1) + indiceTextura] = 1 - ((porciento- porcientos[i])/ porcientos[i+1] - porcientos[i]); 
			pixelColor [x, y, (i) + indiceTextura] = ((porciento- porcientos[i])/ porcientos[i+1] - porcientos[i]); 
			return;
			}
		}

		pixelColor [x, y, 4 + indiceTextura] = 1; 
	}

	IEnumerator ScanTerrenoColor(){ //metodo encargado de pasar por cada pixel, poner su altura, color y refrescar el terreno completo

		//LimitesContorno();
		
		int posArray; //posicion actual en el array de alturas 
		
		int es = 320 * velRefrescado; //  escalado de ancho puede ser calquier numero divisible entre el ancho aun si es menor; debe ser multiplicado por una numero divisible entre el alto
		
		int rf = ancho; 
		int y = es / ancho;
		pixel = new float[ancho,y]; //se inicializa el array actual de cuantos pixeles se van a cambiar por tiro, osea cuantes lineas de y...
		pixelColor = new float[ancho,y,splatWeights.Length];  //se inicializa el array actual de cuantos colores pixeles se van a cambiar por tiro, osea cuantes lineas de y...
		float nm;  //variable de la altura de 0 a 4 metros mapeada a un porcentaje de 0 a 1 

		for (int i = 0; i < tamaño; i+=es) { //se pasa por todo el array de alturas en cantidades de escalado
			
			for (int j = 0; j < y; j++) { //se pasa por cada alto y ancho de esa cantidad de escalado, por ejemplo 120 se pasara por cada pixel de una linea de y
				
				for (int c = 0; c < ancho; c++) {

					posArray = i + (c + (ancho*j)); //se mapea el x y a el punto en el array de alturas

					topoMap.SetPixel((posArray/rf), (posArray%rf), bkgColor);

					if((Mathf.Abs(depthImg[posArray] - depthImgAnt[posArray]) > tolerancia) && depthImg[posArray] > 0){ //si la diferencia en la altura actual y la anterior es mayor a la diferencia y el valor es mayor a 0 se hace lo siguiente
					
					nm =  depthImg[posArray].Remap (4000, 0, 0, 1); //se mapea el depth a un numero de 0 a 1
					pixel [c, j] = nm; //se guarda el porcentaje en el pixel de altura actual
					SetColor2(nm,c,j); //se manda este porcentaje para ser coloreado en el metodo set color
					depthImgAnt[posArray] = depthImg[posArray]; //se guarda el pixel actual para ser usado como anterior en la siguiente iteracion
					}

					else{ //si no cumple con lo anterior su usara el mismo pixel que se uso la pasada anterior
					nm = depthImgAnt[posArray].Remap (4000, 0, 0, 1);
						pixel [c, j] = nm; //se pone como altura
						SetColor2(nm,c,j); //y se colorea
					}
				}				
			}
			tData.SetAlphamaps((i/rf),(i%rf),pixelColor); //se actualizan los datos del terreno por cada pasada
			tData.SetHeights((i/rf),(i%rf),pixel);
			//yield return new WaitForSeconds(5.0f);
			yield return 0;
		}
		corrutina = false;
	}

	void Update () {

		if (Input.GetKeyDown (KeyCode.R)) {
		
			DefaultDistancias();
		
		}

		if (Input.GetKeyDown (KeyCode.A)) { //si se pulsa la A se usan las texturas de solo colores

			lineas = !lineas;
			Cubo.SetActive(lineas);
			indiceTextura = 5;
			peces.SetActive(false); 

		}

		if (Input.GetKeyDown (KeyCode.D)) { //si se pulsa la D se usan las texturas reales
			peces.SetActive(true); 
			indiceTextura = 0;
			Cubo.SetActive(false);
			lineas = false;
		}


		if (kinect.pollDepth ()) {

				if(!corrutina){ //si la corrutina anterior termino de correr
					GetDepth (kinect.getDepth ()); //se carga el array de alturas 
					//LimitesContorno();
					corrutina = true; //se pone el flag diciendo que esta en uso
					StartCoroutine(ScanTerrenoColor()); //se empieza la corutina
					//Cubo.renderer.material.mainTexture = DibujarCubo();
				}
			}
		}
}
