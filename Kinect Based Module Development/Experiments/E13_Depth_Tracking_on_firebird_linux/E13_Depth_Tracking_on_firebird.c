/*
 * This file is part of the OpenKinect Project. http://www.openkinect.org
 *
 * Copyright (c) 2010 individual OpenKinect contributors. See the CONTRIB file
 * for details.
 *
 * This code is licensed to you under the terms of the Apache License, version
 * 2.0, or, at your option, the terms of the GNU General Public License,
 * version 2.0. See the APACHE20 and GPL2 files for the text of the licenses,
 * or the following URLs:
 * http://www.apache.org/licenses/LICENSE-2.0
 * http://www.gnu.org/licenses/gpl-2.0.txt
 *
 * If you redistribute this file in source form, modified or unmodified, you
 * may:
 *   1) Leave this header intact and distribute it under the same terms,
 *      accompanying it with the APACHE20 and GPL20 files, or
 *   2) Delete the Apache 2.0 clause and accompany it with the GPL2 file, or
 *   3) Delete the GPL v2 clause and accompany it with the APACHE20 file
 * In all cases you must keep the copyright notice intact and include a copy
 * of the CONTRIB file.
 *
 * Binary distributions must follow the binary distribution requirements of
 * either License.
 */

#include <sys/types.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <termios.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <assert.h>
#include "libfreenect.h"

#include <pthread.h>

#if defined(__APPLE__)
#include <GLUT/glut.h>
#include <OpenGL/gl.h>
#include <OpenGL/glu.h>
#else
#include <GL/glut.h>
#include <GL/gl.h>
#include <GL/glu.h>
#endif

#include <math.h>
 
char *tmpbuffer ; // Temporary buffer for holding the data to be sent via zigbee
void openport(void); // Open the Zigbee port and set its characteristics
void sendport(void); // Send the data
        
#define BAUDRATE B115200 
#define MODEMDEVICE "/dev/ttyUSB0" // Zigbee port
#define _POSIX_SOURCE 1 // POSIX compliant source
#define FALSE 0
#define TRUE 1

pthread_t freenect_thread;
volatile int die = 0;

int g_argc;
char **g_argv;

int window;

pthread_mutex_t gl_backbuf_mutex = PTHREAD_MUTEX_INITIALIZER;

// back: owned by libfreenect (implicit for depth)
// mid: owned by callbacks, "latest frame ready"
// front: owned by GL, "currently being drawn"

uint8_t *depth_mid, *depth_front;
uint8_t *rgb_back, *rgb_mid, *rgb_front;

GLuint gl_depth_tex;
GLuint gl_rgb_tex;
GLfloat camera_angle = 0.0;
int camera_rotate = 0;
int tilt_changed = 0;

freenect_context *f_ctx;
freenect_device *f_dev;
int freenect_angle = 0;
int freenect_led;

freenect_video_format requested_format = FREENECT_VIDEO_RGB;
freenect_video_format current_format = FREENECT_VIDEO_RGB;

pthread_cond_t gl_frame_cond = PTHREAD_COND_INITIALIZER;
int got_rgb = 0;
int got_depth = 0;

void DrawGLScene()
{
	pthread_mutex_lock(&gl_backbuf_mutex);

	// When using YUV_RGB mode, RGB frames only arrive at 15Hz, so we shouldn't force them to draw in lock-step.
	// However, this is CPU intensive when we are receiving frames in lockstep.
	if (current_format == FREENECT_VIDEO_YUV_RGB) {
		while (!got_depth && !got_rgb) {
			pthread_cond_wait(&gl_frame_cond, &gl_backbuf_mutex);
		}
	} else {
		while ((!got_depth || !got_rgb) && requested_format != current_format) {
			pthread_cond_wait(&gl_frame_cond, &gl_backbuf_mutex);
		}
	}

	if (requested_format != current_format) {
		pthread_mutex_unlock(&gl_backbuf_mutex);
		return;
	}

	uint8_t *tmp;

	if (got_depth) {
		tmp = depth_front;
		depth_front = depth_mid;
		depth_mid = tmp;
		got_depth = 0;
	}
	if (got_rgb) {
		tmp = rgb_front;
		rgb_front = rgb_mid;
		rgb_mid = tmp;
		got_rgb = 0;
	}

	pthread_mutex_unlock(&gl_backbuf_mutex);
	glBindTexture(GL_TEXTURE_2D, gl_depth_tex);
	glTexImage2D(GL_TEXTURE_2D, 0, 3, 640, 480, 0, GL_RGB, GL_UNSIGNED_BYTE, depth_front);

	if (camera_rotate) {
	    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
	    freenect_raw_tilt_state* state;
	    freenect_update_tilt_state(f_dev);
	    state = freenect_get_tilt_state(f_dev);
	    GLfloat x_accel_raw, x_accel,y_accel_raw,y_accel;
	    x_accel_raw = (GLfloat)state->accelerometer_x/819.0;
	    y_accel_raw = (GLfloat)state->accelerometer_y/819.0;

	    // sloppy acceleration vector cleanup
	    GLfloat accel_length = sqrt(x_accel_raw * x_accel_raw + y_accel_raw * y_accel_raw);
	    x_accel = x_accel_raw/accel_length;
	    y_accel = y_accel_raw/accel_length;
	    camera_angle = atan2(y_accel,x_accel)*180/M_PI -90.0;
	}
	else {
		camera_angle = 0;
	}

	glLoadIdentity();
	glPushMatrix();
	glTranslatef((640.0/2.0),(480.0/2.0) ,0.0);
	glRotatef(camera_angle, 0.0, 0.0, 1.0);
	glTranslatef(-(640.0/2.0),-(480.0/2.0) ,0.0);
	glBegin(GL_TRIANGLE_FAN);
	glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
	glTexCoord2f(0, 1); glVertex3f(0,0,1.0);
	glTexCoord2f(1, 1); glVertex3f(640,0,1.0);
	glTexCoord2f(1, 0); glVertex3f(640,480,1.0);
	glTexCoord2f(0, 0); glVertex3f(0,480,1.0);
	glEnd();
	glPopMatrix();

	glBindTexture(GL_TEXTURE_2D, gl_rgb_tex);
	if (current_format == FREENECT_VIDEO_RGB || current_format == FREENECT_VIDEO_YUV_RGB)
		glTexImage2D(GL_TEXTURE_2D, 0, 3, 640, 480, 0, GL_RGB, GL_UNSIGNED_BYTE, rgb_front);
	else
		glTexImage2D(GL_TEXTURE_2D, 0, 1, 640, 480, 0, GL_LUMINANCE, GL_UNSIGNED_BYTE, rgb_front+640*4);

	glPushMatrix();
	glTranslatef(640+(640.0/2.0),(480.0/2.0) ,0.0);
	glRotatef(camera_angle, 0.0, 0.0, 1.0);
	glTranslatef(-(640+(640.0/2.0)),-(480.0/2.0) ,0.0);

	glBegin(GL_TRIANGLE_FAN);
	glColor4f(1.0f, 1.0f, 1.0f, 1.0f);
	glTexCoord2f(0, 1); glVertex3f(640,0,0);
	glTexCoord2f(1, 1); glVertex3f(1280,0,0);
	glTexCoord2f(1, 0); glVertex3f(1280,480,0);
	glTexCoord2f(0, 0); glVertex3f(640,480,0);
	glEnd();
	glPopMatrix();
	glutSwapBuffers();
}

void ReSizeGLScene(int Width, int Height)
{
	glViewport(0,0,Width,Height);
	glMatrixMode(GL_PROJECTION);
	glLoadIdentity();
	glOrtho (0, 1280, 0, 480, -5.0f, 5.0f);
	glMatrixMode(GL_MODELVIEW);
    glLoadIdentity();

}

void InitGL(int Width, int Height)
{
	glClearColor(0.0f, 0.0f, 0.0f, 0.0f);
	//glClearDepth(0.0);
	//glDepthFunc(GL_LESS);
	//glDepthMask(GL_FALSE);
	glDisable(GL_DEPTH_TEST);
	glDisable(GL_BLEND);
	glDisable(GL_ALPHA_TEST);
	glEnable(GL_TEXTURE_2D);
	glBlendFunc (GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
	glShadeModel(GL_FLAT);

	glGenTextures(1, &gl_depth_tex);
	glBindTexture(GL_TEXTURE_2D, gl_depth_tex);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

	glGenTextures(1, &gl_rgb_tex);
	glBindTexture(GL_TEXTURE_2D, gl_rgb_tex);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

	ReSizeGLScene(Width, Height);
}

void *gl_threadfunc(void *arg)
{
	printf("GL thread\n");

	glutInit(&g_argc, g_argv);

	glutInitDisplayMode(GLUT_RGBA | GLUT_DOUBLE | GLUT_ALPHA | GLUT_DEPTH);
	glutInitWindowSize(1280, 480);
	glutInitWindowPosition(0, 0);

	window = glutCreateWindow("LibFreenect");

	glutDisplayFunc(&DrawGLScene);
	glutIdleFunc(&DrawGLScene);
	glutReshapeFunc(&ReSizeGLScene);

	InitGL(1280, 480);

	glutMainLoop();

	return NULL;
}

uint16_t t_gamma[2048];


// This function obtains the depth value based on which it 
// assigns Red Green or Blue color to the pixels.
// Also the direction of firebird (Forward or Backward) 
// is transmitted over zigbee port
void depth_cb(freenect_device *dev, void *v_depth, uint32_t timestamp)
{
	int i;
	uint16_t *depth = (uint16_t*)v_depth;

	pthread_mutex_lock(&gl_backbuf_mutex);
	for (i=0; i<640*480; i++) {
		int pval = t_gamma[depth[i]];
		int lb = pval & 0xff;
		if(i==480*640/2 - 640/2)
		{
		printf("\n%d",(pval>>8));
		if(((pval>>8) <=2) &&((pval>>8) >= 0))
		{
		tmpbuffer = "58";
		printf("\nforward");
		sendport();
		}
		else
		{
		tmpbuffer = "52";
		printf("\nback");
		sendport();
		}
		}
		switch (pval>>8) {
			case 0: 	
				depth_mid[3*i+0] = 255;
				depth_mid[3*i+1] = 0;
				depth_mid[3*i+2] = 0;
				break;
			case 1:
				depth_mid[3*i+0] = 255;
				depth_mid[3*i+1] = 0;
				depth_mid[3*i+2] = 0;
				break;
			case 2:
				depth_mid[3*i+0] = 0;
				depth_mid[3*i+1] = 255;
				depth_mid[3*i+2] = 0;
				break;
			case 3:
				depth_mid[3*i+0] = 0;
				depth_mid[3*i+1] = 255;
				depth_mid[3*i+2] = 0;
				break;
			case 4:
				depth_mid[3*i+0] = 0;
				depth_mid[3*i+1] = 255;
				depth_mid[3*i+2] = 0;
				break;
			case 5:
				depth_mid[3*i+0] = 0;
				depth_mid[3*i+1] = 0;
				depth_mid[3*i+2] = 255;
				break;
			default:
				depth_mid[3*i+0] = 0;
				depth_mid[3*i+1] = 0;
				depth_mid[3*i+2] = 255;
				break;
		}
	}

	got_depth++;
	pthread_cond_signal(&gl_frame_cond);
	pthread_mutex_unlock(&gl_backbuf_mutex);
}

void rgb_cb(freenect_device *dev, void *rgb, uint32_t timestamp)
{
	pthread_mutex_lock(&gl_backbuf_mutex);

	// swap buffers
	assert (rgb_back == rgb);
	rgb_back = rgb_mid;
	freenect_set_video_buffer(dev, rgb_back);
	rgb_mid = (uint8_t*)rgb;

	got_rgb++;
	pthread_cond_signal(&gl_frame_cond);
	pthread_mutex_unlock(&gl_backbuf_mutex);
}

void *freenect_threadfunc(void *arg)
{
	int accelCount = 0;

	freenect_set_tilt_degs(f_dev,freenect_angle);
	freenect_set_led(f_dev,LED_RED);
	freenect_set_depth_callback(f_dev, depth_cb);
	freenect_set_video_callback(f_dev, rgb_cb);
	freenect_set_video_mode(f_dev, freenect_find_video_mode(FREENECT_RESOLUTION_MEDIUM, current_format));
	freenect_set_depth_mode(f_dev, freenect_find_depth_mode(FREENECT_RESOLUTION_MEDIUM, FREENECT_DEPTH_11BIT));
	freenect_set_video_buffer(f_dev, rgb_back);

	freenect_start_depth(f_dev);
	freenect_start_video(f_dev);

	while (!die && freenect_process_events(f_ctx) >= 0) {
		//Throttle the text output
		if (accelCount++ >= 2000)
		{
			accelCount = 0;
			freenect_raw_tilt_state* state;
			freenect_update_tilt_state(f_dev);
			state = freenect_get_tilt_state(f_dev);
			double dx,dy,dz;
			freenect_get_mks_accel(state, &dx, &dy, &dz);;
			fflush(stdout);
		}

		if (requested_format != current_format) {
			freenect_stop_video(f_dev);
			freenect_set_video_mode(f_dev, freenect_find_video_mode(FREENECT_RESOLUTION_MEDIUM, requested_format));
			freenect_start_video(f_dev);
			current_format = requested_format;
		}
	}

	//printf("\nshutting down streams...\n");

	freenect_stop_depth(f_dev);
	freenect_stop_video(f_dev);

	freenect_close_device(f_dev);
	freenect_shutdown(f_ctx);

	//printf("-- done!\n");
	return NULL;
}

int main(int argc, char **argv)
{

	openport();
	sleep(1);
	int res;

	depth_mid = (uint8_t*)malloc(640*480*3);
	depth_front = (uint8_t*)malloc(640*480*3);
	rgb_back = (uint8_t*)malloc(640*480*3);
	rgb_mid = (uint8_t*)malloc(640*480*3);
	rgb_front = (uint8_t*)malloc(640*480*3);

	printf("Kinect camera test\n");

	int i;
	for (i=0; i<2048; i++) {
		float v = i/2048.0;
		v = powf(v, 3)* 6;
		t_gamma[i] = v*6*256;
	}

	g_argc = argc;
	g_argv = argv;

	if (freenect_init(&f_ctx, NULL) < 0) {
		printf("freenect_init() failed\n");
		return 1;
	}

	freenect_set_log_level(f_ctx, FREENECT_LOG_DEBUG);
	freenect_select_subdevices(f_ctx, (freenect_device_flags)(FREENECT_DEVICE_MOTOR | FREENECT_DEVICE_CAMERA));

	int nr_devices = freenect_num_devices (f_ctx);
	printf ("Number of devices found: %d\n", nr_devices);

	int user_device_number = 0;
	if (argc > 1)
		user_device_number = atoi(argv[1]);

	if (nr_devices < 1) {
		freenect_shutdown(f_ctx);
		return 1;
	}

	if (freenect_open_device(f_ctx, &f_dev, user_device_number) < 0) {
		printf("Could not open device\n");
		freenect_shutdown(f_ctx);
		return 1;
	}

	res = pthread_create(&freenect_thread, NULL, freenect_threadfunc, NULL);
	if (res) {
		printf("pthread_create failed\n");
		freenect_shutdown(f_ctx);
		return 1;
	}

	// OS X requires GLUT to run on the main thread
	gl_threadfunc(NULL);

	return 0;
}



FILE *file;
int fileLen;

int fd=0;
struct termios oldtp, newtp;
char buffer[512];
 
void sendport(void)
{
       int n;                
		n = write(fd, tmpbuffer, strlen(tmpbuffer));
 
                if (n < 0)
                {
                        fputs("write() of bytes failed!\n", stderr);
                }
                else
                {
                        printf("Image sent successfully %d\n",n);
                }

 
}
 
void openport(void)
{
         fd = open(MODEMDEVICE, O_RDWR | O_NOCTTY |O_NDELAY );
	printf("Initializing Zigbee %d\n",fd);
         if (fd <0)
         {
         perror(MODEMDEVICE);         }
                                                                                
         fcntl(fd,F_SETFL,0);
        tcgetattr(fd,&oldtp); /* save current serial port settings */
        // tcgetattr(fd,&newtp); /* save current serial port settings */
         bzero(&newtp, sizeof(newtp));
        // bzero(&oldtp, sizeof(oldtp));
                                                                                
         newtp.c_cflag = BAUDRATE | CRTSCTS | CS8 | CLOCAL | CREAD;
                                                                                
         newtp.c_iflag = IGNPAR | ICRNL;
                                                                                
         newtp.c_oflag = 0;
                                                                                
         newtp.c_lflag = ICANON;
                                                                                
         newtp.c_cc[VINTR]    = 0;     /* Ctrl-c */
         newtp.c_cc[VQUIT]    = 0;     /* Ctrl-\ */
         newtp.c_cc[VERASE]   = 0;     /* del */
         newtp.c_cc[VKILL]    = 0;     /* @ */
         //newtp.c_cc[VEOF]     = 4;     /* Ctrl-d */
         newtp.c_cc[VEOF]     = 0;     /* Ctrl-d */
         newtp.c_cc[VTIME]    = 0;     /* inter-character timer unused */
         newtp.c_cc[VMIN]     = 1;     /* blocking read until 1 character arrives */
         newtp.c_cc[VSWTC]    = 0;     /* '\0' */
         newtp.c_cc[VSTART]   = 0;     /* Ctrl-q */
         newtp.c_cc[VSTOP]    = 0;     /* Ctrl-s */
         newtp.c_cc[VSUSP]    = 0;     /* Ctrl-z */
         newtp.c_cc[VEOL]     = 0;     /* '\0' */
         newtp.c_cc[VREPRINT] = 0;     /* Ctrl-r */
         newtp.c_cc[VDISCARD] = 0;     /* Ctrl-u */
         newtp.c_cc[VWERASE]  = 0;     /* Ctrl-w */
         newtp.c_cc[VLNEXT]   = 0;     /* Ctrl-v */
         newtp.c_cc[VEOL2]    = 0;     /* '\0' */
                                                                                
                                                                                
}
