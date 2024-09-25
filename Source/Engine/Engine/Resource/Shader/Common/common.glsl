vec2 Normal3Dto2D(vec3 Normal)
{   
    Normal.xy /= dot( vec3(1.0f), abs(Normal) );
    if( Normal.z <= 0.0f )
    {
        vec2 add;
        if (Normal.x >= 0.0f)
            add.x = 1.0f;
        else 
            add.x = -1.0f;
        if (Normal.y >= 0.0f)
            add.y = 1.0f;
        else 
            add.y = -1.0f;
        Normal.xy = ( 1.0f - abs(Normal.yx) ) * add ;
    }
    return Normal.xy;
}

vec3 Normal2DTo3D(vec2 Oct)
{
	vec3 N = vec3( Oct, 1.0 - dot( vec2(1.0f), abs(Oct) ) );
    if( N.z < 0.0f )
    {
		vec2 add;
		if (N.x >= 0.0f)
			add.x = 1.0f;
		else
			add.x = -1.0f;

		if (N.y >= 0.0f)
			add.y = 1.0f;
		else
			add.y = -1.0f;
		N.xy = ( 1.0f - abs(N.yx) ) * add;
    }
    return normalize(N);
}